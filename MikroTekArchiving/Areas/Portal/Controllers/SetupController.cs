using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.TemplateData;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Reference data and settings: payees, UACS accounts, checklist templates, users, school profile.</summary>
public class SetupController(IUsers users, IFundBudgets budgets) : PortalControllerBase
{
    [Authorize(Roles = PortalRoles.Encoders)]
    public IActionResult Payees() => View(SampleData.Payees);

    [Authorize(Roles = PortalRoles.Encoders)]
    public IActionResult Accounts() => View(SampleData.Accounts);

    [Authorize(Roles = PortalRoles.Admin)]
    public IActionResult Checklists() => View(SampleData.Templates);

    [Authorize(Roles = PortalRoles.Admin)]
    public IActionResult Checklist(int id) => View(SampleData.Templates.FirstOrDefault(t => t.Id == id) ?? SampleData.Templates[0]);

    [Authorize(Roles = PortalRoles.Admin)]
    public IActionResult School() => View();

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = PortalRoles.Encoders)]
    public IActionResult Save(string back)
    {
        // Payees can be saved by bookkeepers; everything else in Setup is admin-only.
        if (back != nameof(Payees) && !User.IsAdmin()) return Forbid();
        return TemplateSaved("Changes saved", back);
    }

    // ---------------------------------------------------------------- Fund budgets (real data)

    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Budgets(int? year) =>
        View(await budgets.GetForYearAsync(year is >= 2000 and <= 2100 ? year.Value : DateTime.Today.Year));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> SaveBudgets(SaveFundBudgetsRequest request)
    {
        var result = await budgets.SaveAsync(request, User.UserId());
        if (result.Success) Toast($"Fund budgets for {request.Year} saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Budgets), new { year = request.Year });
    }

    // ---------------------------------------------------------------- Staff users (real data)

    [Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> Users() => View(await users.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> SaveUser(SaveUserRequest request)
    {
        var result = await users.SaveAsync(request, User.UserId());
        if (result.Success) Toast(request.Id == 0 ? $"Staff account \"{request.Username}\" added." : $"Changes to \"{request.Username}\" saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = PortalRoles.Admin)]
    public async Task<IActionResult> ResetPassword(long id, string? newPassword)
    {
        var result = await users.ResetPasswordAsync(id, newPassword);
        if (result.Success) Toast("Password reset. Give the new password to the person privately.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Users));
    }
}
