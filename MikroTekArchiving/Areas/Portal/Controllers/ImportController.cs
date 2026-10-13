using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>
/// Import MOOE Packet: one scanned PDF of a finished packet (MOOE slip, Alphalist, Cash Disbursements Register)
/// becomes the cash advance and all its DVs.
///  1. Upload → the server reads the pages (offline, with Tesseract) and checks the figures against each other.
///  2. Review → everything read is shown beside the scan; unclear or disagreeing values are marked to check.
///  3. Save → the cash advance and the ticked DVs are saved together, and the PDF is attached to the cash advance.
/// </summary>
[Permission(Permissions.CashAdvancesEncode), Permission(Permissions.VouchersEncode)]
public class ImportController(IPacketImport import, IUacsAccounts accounts) : PortalControllerBase
{
    public async Task<IActionResult> Index() => View(new ImportPage(await import.GetStatusAsync()));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            ToastError("Please choose the scanned packet first.");
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var (result, draft) = await import.ReadAsync(file.FileName, file.Length, stream, User.UserId());
        if (!result.Success || draft is null)
        {
            ToastError(result.Error ?? "The file could not be read.");
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Review), new { token = draft.Token });
    }

    [HttpGet]
    public async Task<IActionResult> Review(string token)
    {
        var draft = await import.GetDraftAsync(token);
        if (draft is null) return Gone();
        return View(await ReviewAsync(draft));
    }

    /// <summary>The uploaded packet, shown beside the review form.</summary>
    [HttpGet]
    public IActionResult Packet(string token)
    {
        var path = import.GetPacketPath(token);
        if (path is null) return NotFound("This packet is no longer waiting for review.");

        var pdf = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline").ToString();
        return PhysicalFile(path, pdf ? "application/pdf" : path.EndsWith(".png") ? "image/png" : "image/jpeg", enableRangeProcessing: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PacketDraft draft)
    {
        var result = await import.SaveAsync(draft, User.UserId());
        if (!result.Success)
        {
            // Show the form again as it was typed, with the notes from the reading.
            var original = await import.GetDraftAsync(draft.Token);
            if (original is null)
            {
                ToastError(result.Error!);
                return RedirectToAction(nameof(Index));
            }
            KeepNotes(draft, original);
            return View("Review", await ReviewAsync(draft, result.Error));
        }

        var saved = draft.Vouchers.Count(v => v.Include);
        Toast(draft.CashAdvance.ExistingId == 0
            ? $"Cash advance and {saved} DV(s) saved from the packet. The PDF is attached to the cash advance."
            : $"{saved} DV(s) added to the cash advance. The PDF is attached to it.");
        return RedirectToAction("Details", "CashAdvances", new { id = result.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Discard(string token)
    {
        import.Discard(token);
        Toast("The packet was discarded. Nothing was saved.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<ImportReview> ReviewAsync(PacketDraft draft, string? error = null) => new()
    {
        Draft = draft,
        Accounts = (await accounts.GetAllAsync()).Where(a => a.IsActive).ToList(),
        Error = error
    };

    /// <summary>The form does not send the reading notes back; take them from the stored reading (same DVs, same order).</summary>
    private static void KeepNotes(PacketDraft typed, PacketDraft original)
    {
        typed.FileName = original.FileName;
        typed.PageCount = original.PageCount;
        typed.PageKinds = original.PageKinds;
        typed.Notes = original.Notes;
        typed.Seconds = original.Seconds;
        typed.RegisterTotalPayments = original.RegisterTotalPayments;
        typed.CashAdvance.Flags = original.CashAdvance.Flags;
        for (var i = 0; i < typed.Vouchers.Count && i < original.Vouchers.Count; i++)
        {
            typed.Vouchers[i].Flags = original.Vouchers[i].Flags;
            typed.Vouchers[i].Payment = original.Vouchers[i].Payment;
            typed.Vouchers[i].Balance = original.Vouchers[i].Balance;
        }
    }

    private IActionResult Gone()
    {
        ToastError("That packet is no longer waiting for review (it was saved, discarded or is more than 2 days old).");
        return RedirectToAction(nameof(Index));
    }
}
