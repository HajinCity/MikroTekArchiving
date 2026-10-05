using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

[Permission(Permissions.VouchersView)]
public class VouchersController(IDisbursements vouchers, ICashAdvances advances, IChecklistTemplates templates,
    IUacsAccounts accounts, IPayees payees, ISchoolProfile school, IArchiveFiles files) : PortalControllerBase
{
    public async Task<IActionResult> Index(string? q, string? dept, string? status, long? ca, DateTime? from)
    {
        var list = (await vouchers.GetAllAsync(includeDrafts: true)).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
            list = list.Where(v => $"{v.DvNo} {v.Payee} {v.Particulars} {v.CheckNo}".Contains(q.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(dept)) list = list.Where(v => v.Dept == dept);
        if (ca is not null) list = list.Where(v => v.CashAdvanceId == ca);
        if (from is not null) list = list.Where(v => v.Date >= from.Value.Date);
        if (status == "complete") list = list.Where(v => v.IsComplete);
        if (status == "incomplete") list = list.Where(v => !v.IsDraft && !v.IsComplete);
        if (status == "draft") list = list.Where(v => v.IsDraft);
        return View(list.OrderByDescending(v => v.Date).ThenByDescending(v => v.DvNo).ToList());
    }

    public async Task<IActionResult> Details(long id)
    {
        var dv = await vouchers.GetAsync(id);
        if (dv is null) return Gone();
        // A draft has nothing to show yet: go straight to encoding it.
        if (dv.IsDraft)
            return User.Can(Permissions.VouchersEncode) ? RedirectToAction(nameof(Edit), new { id }) : Gone();

        ViewData["CashAdvance"] = await advances.GetAsync(dv.CashAdvanceId);
        ViewData["School"] = await school.GetAsync();
        ViewData["Files"] = new FilesPanel(FileOwner.Voucher, dv.Id, await files.GetForOwnerAsync(FileOwner.Voucher, dv.Id),
            CanEdit: User.Can(Permissions.VouchersEncode));
        return View(dv);
    }

    /// <summary>Printable archive record: DV details + checklist cover sheet.</summary>
    public async Task<IActionResult> Print(long id)
    {
        var dv = await vouchers.GetAsync(id);
        if (dv is null || dv.IsDraft) return Gone();

        ViewData["CashAdvance"] = await advances.GetAsync(dv.CashAdvanceId);
        ViewData["School"] = await school.GetAsync();
        return View(dv);
    }

    // ── Scan-first flow ───────────────────────────────────────────────
    // 1. Upload the scanned packet → a draft DV is created with the file attached.
    // 2. The encoding form opens with the scan beside it; the bookkeeper types what is on the paper.
    // 3. Saving turns the draft into an archived DV.
    // (Reading the text from the scan automatically comes later.)

    [HttpGet, Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> Upload(long? ca) => View(await UploadPageAsync(ca));

    [HttpPost, ValidateAntiForgeryToken, ActionName("Upload"), Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> UploadPost(long cashAdvanceId, long? type, [FromForm(Name = "files")] List<IFormFile>? uploads)
    {
        var picked = (uploads ?? []).Where(f => f.Length > 0).ToList();
        if (picked.Count == 0)
        {
            ToastError("Please choose the scanned file first.");
            return RedirectToAction(nameof(Upload), new { ca = cashAdvanceId });
        }

        var draft = await vouchers.CreateDraftAsync(cashAdvanceId, type, User.UserId());
        if (!draft.Success)
        {
            ToastError(draft.Error!);
            return RedirectToAction(nameof(Upload));
        }

        var (stored, problems) = await StoreAsync(picked, draft.Id);
        if (stored == 0)
        {
            // Nothing usable was uploaded, so do not leave an empty draft behind.
            await vouchers.DeleteAsync(draft.Id, User.UserId());
            ToastError(string.Join(" ", problems));
            return RedirectToAction(nameof(Upload), new { ca = cashAdvanceId });
        }
        if (problems.Count > 0) ToastError(string.Join(" ", problems));
        Toast("Scan uploaded. Type the details from the scan, then save to archive it.");
        return RedirectToAction(nameof(Edit), new { id = draft.Id });
    }

    /// <summary>Manual encoding (when there is no scan yet).</summary>
    [HttpGet, Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> Create(long? ca)
    {
        var all = await advances.GetAllAsync();
        var chosen = all.FirstOrDefault(c => c.Id == ca) ?? all.FirstOrDefault(c => c.IsOpen) ?? all.FirstOrDefault();
        var s = await school.GetAsync();
        return View("Edit", await FormAsync(new SaveDvRequest
        {
            CashAdvanceId = chosen?.Id ?? 0, Date = DateTime.Today, Mode = "Check", MfoPap = "MOOE",
            FundCluster = chosen?.FundCluster is { Length: > 0 } cluster ? cluster : s.FundCluster,
            ResponsibilityCenter = s.ResponsibilityCenter, Bank = chosen is null ? "" : s.BankFor(chosen.Dept),
            Lines = [new DvLineInput()]
        }, existing: null));
    }

    [HttpGet, Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> Edit(long id)
    {
        var dv = await vouchers.GetAsync(id);
        if (dv is null) return Gone();

        var s = await school.GetAsync();
        var values = new SaveDvRequest
        {
            Id = dv.Id, CashAdvanceId = dv.CashAdvanceId, DvNo = dv.DvNo, Date = dv.Date, Type = dv.TemplateId,
            FundCluster = dv.FundCluster, Payee = dv.Payee, Tin = dv.Tin, OrsBurs = dv.OrsBurs, Address = dv.Address,
            Particulars = dv.Particulars, ResponsibilityCenter = dv.ResponsibilityCenter, MfoPap = dv.MfoPap,
            Lines = dv.Lines.Select(l => new DvLineInput { Code = l.Code, Amount = l.Amount }).ToList(),
            Tax1 = dv.Tax1 == 0 ? null : dv.Tax1, Tax3 = dv.Tax3 == 0 ? null : dv.Tax3,
            Mode = dv.Mode, CheckNo = dv.CheckNo, PaidDate = dv.PaidDate, Bank = dv.Bank, ReceivedBy = dv.ReceivedBy,
            OrNo = dv.OrNo, JevNo = dv.JevNo, ChecklistRemarks = dv.ChecklistRemarks,
            Check = dv.Checklist.Select(c => new ChecklistInput { Label = c.Label, Mark = c.Mark, Date = c.Date }).ToList()
        };
        if (dv.IsDraft)
        {
            // A draft only knows its cash advance and type: start the rest from the usual values.
            values.Mode = "Check";
            values.MfoPap = "MOOE";
            values.FundCluster = s.FundCluster;
            values.ResponsibilityCenter = s.ResponsibilityCenter;
            values.Bank = s.BankFor(dv.Dept);
        }
        if (values.Lines.Count == 0) values.Lines.Add(new DvLineInput());
        return View(await FormAsync(values, dv));
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> Save(SaveDvRequest request, [FromForm(Name = "files")] List<IFormFile>? uploads)
    {
        var result = await vouchers.SaveAsync(request, User.UserId());
        if (!result.Success)
        {
            if (request.Lines.Count == 0) request.Lines.Add(new DvLineInput());
            var existing = request.Id == 0 ? null : await vouchers.GetAsync(request.Id);
            return View("Edit", await FormAsync(request, existing, result.Error));
        }

        var (_, problems) = await StoreAsync((uploads ?? []).Where(f => f.Length > 0).ToList(), result.Id);
        if (problems.Count > 0) ToastError(string.Join(" ", problems));
        Toast(request.Id == 0 ? "DV saved to the archive." : "Changes to the DV saved.");
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> SaveChecklist(long id, List<ChecklistInput>? check, string? remarks)
    {
        var result = await vouchers.SaveChecklistAsync(id, check ?? [], remarks, User.UserId());
        if (result.Success) Toast("Checklist updated.");
        else ToastError(result.Error!);
        return Redirect(Url.Action(nameof(Details), new { id }) + "#tab-check");
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.VouchersEncode)]
    public async Task<IActionResult> Delete(long id)
    {
        var dv = await vouchers.GetAsync(id);
        var result = await vouchers.DeleteAsync(id, User.UserId());
        if (!result.Success)
        {
            ToastError(result.Error!);
            return RedirectToAction(nameof(Index));
        }
        Toast(dv?.IsDraft == true ? "Draft discarded." : $"DV {dv?.DvNo} deleted.");
        return dv?.IsDraft == true ? RedirectToAction(nameof(Upload)) : RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- helpers

    private async Task<DvForm> FormAsync(SaveDvRequest values, DvDto? existing, string? error = null)
    {
        var allTemplates = await templates.GetAllAsync();
        var allAccounts = await accounts.GetAllAsync();
        var usedCodes = values.Lines.Select(l => l.Code).ToHashSet();
        return new DvForm
        {
            Values = values, Existing = existing, Error = error,
            CashAdvances = await advances.GetAllAsync(),
            // Only what is in use is offered, plus whatever this DV already has.
            Templates = allTemplates.Where(t => t.IsActive || t.Id == values.Type).ToList(),
            Accounts = allAccounts.Where(a => (a.IsActive && a.Group is AccountGroup.Expense or AccountGroup.Asset) || usedCodes.Contains(a.Code)).ToList(),
            Payees = (await payees.GetAllAsync()).Where(p => p.IsActive).ToList(),
            NextNumbers = await vouchers.GetNextDvNumbersAsync(),
            School = await school.GetAsync(),
            Scans = existing is null ? [] : await files.GetForOwnerAsync(FileOwner.Voucher, existing.Id)
        };
    }

    private async Task<UploadPage> UploadPageAsync(long? ca) => new()
    {
        CashAdvances = await advances.GetAllAsync(),
        Templates = (await templates.GetAllAsync()).Where(t => t.IsActive).ToList(),
        Drafts = (await vouchers.GetAllAsync(includeDrafts: true)).Where(v => v.IsDraft).OrderByDescending(v => v.EncodedAt).ToList(),
        SelectedCashAdvanceId = ca
    };

    /// <summary>Stores the uploaded scans on the DV. Returns how many were stored and what was refused.</summary>
    private async Task<(int Stored, List<string> Problems)> StoreAsync(List<IFormFile> uploads, long dvId)
    {
        var stored = 0;
        var problems = new List<string>();
        foreach (var f in uploads)
        {
            await using var stream = f.OpenReadStream();
            var result = await files.AddAsync(FileOwner.Voucher, dvId, f.FileName, f.Length, stream, User.UserId());
            if (result.Success) stored++;
            else problems.Add(result.Error!);
        }
        return (stored, problems);
    }

    private IActionResult Gone()
    {
        ToastError("That DV is not in the archive (it may have been deleted).");
        return RedirectToAction(nameof(Index));
    }
}
