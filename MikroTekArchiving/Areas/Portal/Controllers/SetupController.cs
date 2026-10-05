using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Reference data: payees, UACS accounts, fund budgets, checklist templates, school profile.</summary>
public class SetupController(IFundBudgets budgets, IUacsAccounts accounts, IPayees payees,
    IChecklistTemplates templates, ISchoolProfile school) : PortalControllerBase
{
    // ---------------------------------------------------------------- Payees

    [Permission(Permissions.SetupPayees)]
    public async Task<IActionResult> Payees() => View(await payees.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SetupPayees)]
    public async Task<IActionResult> SavePayee(SavePayeeRequest request)
    {
        var result = await payees.SaveAsync(request, User.UserId());
        if (result.Success) Toast(request.Id == 0 ? "Payee added." : "Changes to the payee saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Payees));
    }

    // ---------------------------------------------------------------- UACS accounts

    [Permission(Permissions.SetupAccounts)]
    public async Task<IActionResult> Accounts() => View(await accounts.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SetupAccounts)]
    public async Task<IActionResult> SaveAccount(SaveUacsAccountRequest request)
    {
        var result = await accounts.SaveAsync(request, User.UserId());
        if (result.Success) Toast(request.Id == 0 ? "Account added." : "Changes to the account saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Accounts));
    }

    // ---------------------------------------------------------------- Checklist templates

    [Permission(Permissions.SetupChecklists)]
    public async Task<IActionResult> Checklists() => View(await templates.GetAllAsync());

    /// <summary>id = 0 opens an empty form for a new checklist type.</summary>
    [Permission(Permissions.SetupChecklists)]
    public async Task<IActionResult> Checklist(long id)
    {
        if (id == 0) return View(new ChecklistTemplateDto(0, "", true, [""], 0));

        var template = await templates.GetAsync(id);
        if (template is not null) return View(template);
        ToastError("That checklist type no longer exists.");
        return RedirectToAction(nameof(Checklists));
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SetupChecklists)]
    public async Task<IActionResult> SaveChecklist(SaveChecklistTemplateRequest request)
    {
        var result = await templates.SaveAsync(request, User.UserId());
        if (!result.Success)
        {
            // Show the form again with what was typed, so nothing is lost.
            ViewData["Error"] = result.Error;
            var typed = (request.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            return View(nameof(Checklist), new ChecklistTemplateDto(request.Id, request.Name ?? "", request.IsActive, typed.Count == 0 ? [""] : typed, 0));
        }
        Toast("Checklist type saved.");
        return RedirectToAction(nameof(Checklists));
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SetupChecklists)]
    public async Task<IActionResult> DeleteChecklist(long id)
    {
        var result = await templates.DeleteAsync(id, User.UserId());
        if (result.Success) Toast("Checklist type deleted. DVs already encoded keep their checklist.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Checklists));
    }

    // ---------------------------------------------------------------- School & signatories

    [Permission(Permissions.SetupSchool)]
    public async Task<IActionResult> School() => View(await school.GetAsync());

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SetupSchool)]
    public async Task<IActionResult> SaveSchool(SchoolProfileDto profile)
    {
        var result = await school.SaveAsync(profile, User.UserId());
        if (result.Success) Toast("School profile and signatories saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(School));
    }

    // ---------------------------------------------------------------- Fund budgets

    [Permission(Permissions.SetupBudgets)]
    public async Task<IActionResult> Budgets(int? year) =>
        View(await budgets.GetForYearAsync(year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year));

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SetupBudgets)]
    public async Task<IActionResult> SaveBudgets(SaveFundBudgetsRequest request)
    {
        var result = await budgets.SaveAsync(request, User.UserId());
        if (result.Success) Toast($"Fund budgets for {request.Year} saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Budgets), new { year = request.Year });
    }
}
