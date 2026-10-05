using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

[Permission(Permissions.CashAdvancesView)]
public class CashAdvancesController(ICashAdvances advances, IArchiveFiles files, ISchoolProfile school) : PortalControllerBase
{
    public async Task<IActionResult> Index() => View(await advances.GetAllAsync());

    public async Task<IActionResult> Details(long id)
    {
        var ca = await advances.GetAsync(id);
        if (ca is null) return Gone();

        ViewData["Files"] = new FilesPanel(FileOwner.CashAdvance, ca.Id, await files.GetForOwnerAsync(FileOwner.CashAdvance, ca.Id),
            CanEdit: User.Can(Permissions.CashAdvancesEncode), Compact: true);
        return View(ca);
    }

    /// <summary>Empty form; names and fund cluster start from Setup → School &amp; Signatories.</summary>
    [HttpGet, Permission(Permissions.CashAdvancesEncode)]
    public async Task<IActionResult> Create()
    {
        var s = await school.GetAsync();
        var last = (await advances.GetAllAsync()).FirstOrDefault();
        return View("Edit", new CashAdvanceForm
        {
            Values = new SaveCashAdvanceRequest
            {
                BatchNo = (last?.BatchNo ?? 0) + 1, Date = DateTime.Today, FundCluster = s.FundCluster,
                Officer = s.HeadName, OfficerPosition = s.HeadPosition,
                VerifiedBy = s.CashUnitName, VerifiedByPosition = s.CashUnitPosition
            }
        });
    }

    [HttpGet, Permission(Permissions.CashAdvancesEncode)]
    public async Task<IActionResult> Edit(long id)
    {
        var ca = await advances.GetAsync(id);
        if (ca is null) return Gone();

        return View(new CashAdvanceForm
        {
            Values = new SaveCashAdvanceRequest
            {
                Id = ca.Id, Dept = Enum.Parse<Department>(ca.Dept), Fund = ca.Fund, BatchNo = ca.BatchNo,
                PayrollNo = ca.PayrollNo, AdaNo = ca.AdaNo, Date = ca.Date, Amount = ca.Amount, Period = ca.Period,
                FundCluster = ca.FundCluster, Officer = ca.Officer, OfficerPosition = ca.OfficerPosition,
                VerifiedBy = ca.VerifiedBy, VerifiedByPosition = ca.VerifiedByPosition
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.CashAdvancesEncode)]
    public async Task<IActionResult> Save(SaveCashAdvanceRequest request, [FromForm(Name = "files")] List<IFormFile>? uploads)
    {
        var result = await advances.SaveAsync(request, User.UserId());
        if (!result.Success)
            return View("Edit", new CashAdvanceForm { Values = request, Error = result.Error });

        Toast(request.Id == 0 ? "Cash advance recorded." : "Changes to the cash advance saved.");
        await StoreAsync(uploads, result.Id);
        return RedirectToAction(nameof(Details), new { id = result.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.CashAdvancesEncode)]
    public async Task<IActionResult> SetLiquidated(long id, bool liquidated)
    {
        var result = await advances.SetLiquidatedAsync(id, liquidated, User.UserId());
        if (result.Success) Toast(liquidated ? "Marked as liquidated." : "Reopened: DVs can be charged to it again.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.CashAdvancesEncode)]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await advances.DeleteAsync(id, User.UserId());
        if (result.Success)
        {
            Toast("Cash advance deleted.");
            return RedirectToAction(nameof(Index));
        }
        ToastError(result.Error!);
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task StoreAsync(List<IFormFile>? uploads, long cashAdvanceId)
    {
        var problems = new List<string>();
        foreach (var f in (uploads ?? []).Where(f => f.Length > 0))
        {
            await using var stream = f.OpenReadStream();
            var stored = await files.AddAsync(FileOwner.CashAdvance, cashAdvanceId, f.FileName, f.Length, stream, User.UserId());
            if (!stored.Success) problems.Add(stored.Error!);
        }
        if (problems.Count > 0) ToastError(string.Join(" ", problems));
    }

    private IActionResult Gone()
    {
        ToastError("That cash advance is not in the system (it may have been deleted).");
        return RedirectToAction(nameof(Index));
    }
}
