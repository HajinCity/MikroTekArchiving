using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.TemplateData;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Reference data: payees, UACS accounts, fund budgets, checklist templates, school profile.</summary>
public class SetupController(IFundBudgets budgets, IUacsAccounts accounts) : PortalControllerBase
{
    [Permission(Permissions.SetupPayees)]
    public IActionResult Payees() => View(SampleData.Payees);

    // ---------------------------------------------------------------- UACS accounts (real data)

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

    [Permission(Permissions.SetupChecklists)]
    public IActionResult Checklists() => View(SampleData.Templates);

    [Permission(Permissions.SetupChecklists)]
    public IActionResult Checklist(int id) => View(SampleData.Templates.FirstOrDefault(t => t.Id == id) ?? SampleData.Templates[0]);

    [Permission(Permissions.SetupSchool)]
    public IActionResult School() => View();

    /// <summary>Template save for the sample Setup pages. "back" = the page the form came from.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(string back)
    {
        var needed = back switch
        {
            nameof(Payees) => Permissions.SetupPayees,
            nameof(Checklists) or nameof(Checklist) => Permissions.SetupChecklists,
            nameof(School) => Permissions.SetupSchool,
            _ => null
        };
        if (needed is null || !User.Can(needed)) return Forbid();
        return TemplateSaved("Changes saved", back);
    }

    // ---------------------------------------------------------------- Fund budgets (real data)

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
