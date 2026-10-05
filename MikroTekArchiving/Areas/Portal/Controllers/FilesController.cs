using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.TemplateData;
using MikroTek.Domain;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>
/// Uploading, viewing and removing the scanned documents of a voucher or a cash advance.
/// Who may do what follows the record the file belongs to: viewing needs its "view" access,
/// uploading and removing need its "encode" access.
/// </summary>
public class FilesController(IArchiveFiles files) : PortalControllerBase
{
    /// <summary>Shows the file in the browser (PDF viewer / picture), or downloads it with ?download=true.</summary>
    [HttpGet]
    public async Task<IActionResult> Open(long id, bool download = false)
    {
        var file = await files.GetAsync(id);
        if (file is null) return NotFound("This file is no longer on the server.");
        if (!User.Can(ViewAccess(file.Info.OwnerType))) return Forbid();

        // The browser must show it as the type we checked at upload, never guess another one.
        Response.Headers.XContentTypeOptions = "nosniff";
        if (download)
            return PhysicalFile(file.FullPath, file.Info.ContentType, file.Info.FileName);

        Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = file.Info.FileName }.ToString();
        return PhysicalFile(file.FullPath, file.Info.ContentType, enableRangeProcessing: true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(FileOwner owner, long ownerId, [FromForm(Name = "files")] List<IFormFile>? uploads)
    {
        if (!Enum.IsDefined(owner) || !OwnerExists(owner, ownerId)) return NotFound();
        if (!User.Can(EditAccess(owner))) return Forbid();

        var picked = (uploads ?? []).Where(f => f.Length > 0).ToList();
        if (picked.Count == 0)
        {
            ToastError("Please choose at least one file to upload.");
            return BackTo(owner, ownerId);
        }

        var saved = 0;
        var problems = new List<string>();
        foreach (var f in picked)
        {
            await using var stream = f.OpenReadStream();
            var result = await files.AddAsync(owner, ownerId, f.FileName, f.Length, stream, User.UserId());
            if (result.Success) saved++;
            else problems.Add(result.Error!);
        }

        if (saved > 0) Toast(saved == 1 ? "1 file uploaded." : $"{saved} files uploaded.");
        if (problems.Count > 0) ToastError(string.Join(" ", problems));
        return BackTo(owner, ownerId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var info = await files.GetInfoAsync(id);
        if (info is null) return NotFound();
        if (!User.Can(EditAccess(info.OwnerType))) return Forbid();

        var result = await files.DeleteAsync(id, User.UserId());
        if (result.Success) Toast($"\"{info.FileName}\" was removed.");
        else ToastError(result.Error!);
        return BackTo(info.OwnerType, info.OwnerId);
    }

    private static string ViewAccess(FileOwner owner) =>
        owner == FileOwner.Voucher ? Permissions.VouchersView : Permissions.CashAdvancesView;

    private static string EditAccess(FileOwner owner) =>
        owner == FileOwner.Voucher ? Permissions.VouchersEncode : Permissions.CashAdvancesEncode;

    // Vouchers and cash advances are still sample data; this checks against the same lists the pages show.
    private static bool OwnerExists(FileOwner owner, long id) =>
        owner == FileOwner.Voucher ? SampleData.Vouchers.Any(v => v.Id == id) : SampleData.CashAdvances.Any(c => c.Id == id);

    private IActionResult BackTo(FileOwner owner, long id) =>
        owner == FileOwner.Voucher
            ? Redirect(Url.Action("Details", "Vouchers", new { id }) + "#tab-files")
            : RedirectToAction("Details", "CashAdvances", new { id });
}
