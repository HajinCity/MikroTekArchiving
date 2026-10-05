using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Settings menu: My Account (everyone), Staff Users and Roles &amp; Access (System Admin).</summary>
public class SettingsController(IUsers users, IRoles roles) : PortalControllerBase
{
    // ---------------------------------------------------------------- My Account (every signed-in person)

    public async Task<IActionResult> Account()
    {
        var me = await users.GetAsync(User.UserId());
        return me is null ? RedirectToAction("Login", "Account") : View(me);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAccount(string? name, string? position)
    {
        var result = await users.UpdateOwnProfileAsync(User.UserId(), name, position);
        if (result.Success) Toast("Your account information has been saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Account));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        var result = input.NewPassword != input.ConfirmPassword
            ? ServiceResult.Fail("The new password and its confirmation do not match.")
            : await users.ChangeOwnPasswordAsync(User.UserId(), input.CurrentPassword, input.NewPassword);
        if (!result.Success)
        {
            ToastError(result.Error!);
            return RedirectToAction(nameof(Account));
        }

        // Sign in again (keeping "keep me signed in") so the default/temporary password warnings disappear.
        var wasForced = User.HasTemporaryPassword() || User.UsingDefaultPassword();
        if (await users.GetAsync(User.UserId()) is { } me)
        {
            var current = await HttpContext.AuthenticateAsync(PortalAuth.Scheme);
            await HttpContext.SignInAsync(PortalAuth.Scheme, PortalAuth.CreatePrincipal(me), current.Properties);
        }
        Toast("Your password has been changed.");
        return wasForced ? RedirectToAction("Home", "Account") : RedirectToAction(nameof(Account));
    }

    // ---------------------------------------------------------------- Staff Users (System Admin)

    [Permission(Permissions.SettingsUsers)]
    public async Task<IActionResult> Users() => View(await users.GetAllAsync());

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SettingsUsers)]
    public async Task<IActionResult> SaveUser(SaveUserRequest request)
    {
        var result = await users.SaveAsync(request, User.UserId());
        if (result.Success) Toast(request.Id == 0
            ? $"Staff account \"{request.Username}\" added. They will choose their own password when they first sign in."
            : $"Changes to \"{request.Username}\" saved.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Users));
    }

    /// <summary>Gives an account a temporary password; the person must change it at their next sign-in.</summary>
    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SettingsUsers)]
    public async Task<IActionResult> ResetPassword(long id, string? newPassword)
    {
        var result = await users.ResetPasswordAsync(id, newPassword, User.UserId());
        if (result.Success) Toast(id == User.UserId()
            ? "Your password has been changed."
            : "Temporary password set. Give it to the person privately; they will be asked to choose their own when they sign in.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Users));
    }

    // ---------------------------------------------------------------- Roles & Access (System Admin)

    [Permission(Permissions.SettingsRoles)]
    public async Task<IActionResult> Roles()
    {
        var model = new Dictionary<UserRole, HashSet<string>>();
        foreach (var role in RoleNames.Order)
            model[role] = await roles.GetPermissionsAsync(role);
        return View(model);
    }

    /// <summary>Each ticked box arrives as "Role:feature", e.g. "Viewer:vouchers.view".</summary>
    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.SettingsRoles)]
    public async Task<IActionResult> SaveRoles(string[]? grants)
    {
        var ticks = RoleNames.Order.Where(r => r != UserRole.Admin).ToDictionary(r => r, _ => new List<string>());
        foreach (var grant in grants ?? [])
        {
            var parts = grant.Split(':', 2);
            if (parts.Length == 2 && Enum.TryParse<UserRole>(parts[0], out var role) && ticks.TryGetValue(role, out var list))
                list.Add(parts[1]);
        }

        var result = await roles.SaveAsync(ticks, User.UserId());
        if (result.Success) Toast("Access saved. It applies to everyone right away, no need to sign in again.");
        else ToastError(result.Error!);
        return RedirectToAction(nameof(Roles));
    }
}
