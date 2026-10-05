using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.Interface;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Staff sign-in / sign-out and changing your own password.</summary>
public class AccountController(IUsers users) : PortalControllerBase
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View(new LoginInput { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input)
    {
        var result = await users.SignInAsync(input.Username, input.Password);
        if (!result.Success)
        {
            input.Password = null;
            input.Error = result.Error;
            return View(input);
        }

        var props = new AuthenticationProperties { IsPersistent = input.Remember };
        if (input.Remember) props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);
        await HttpContext.SignInAsync(PortalAuth.Scheme, PortalAuth.CreatePrincipal(result.User!, result.UsingDefaultPassword), props);

        if (result.UsingDefaultPassword)
        {
            ToastError("You signed in with the default admin password. Please change it now.");
            return RedirectToAction(nameof(ChangePassword));
        }
        return Url.IsLocalUrl(input.ReturnUrl) ? Redirect(input.ReturnUrl!) : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await users.SignOutAsync(User.UserId());
        await HttpContext.SignOutAsync(PortalAuth.Scheme);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>Shown when a signed-in person opens a page their role does not allow.</summary>
    [HttpGet]
    public IActionResult Denied() => View();

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (input.NewPassword != input.ConfirmPassword)
        {
            ModelState.AddModelError("", "The new password and its confirmation do not match.");
            return View(new ChangePasswordInput());
        }

        var result = await users.ChangeOwnPasswordAsync(User.UserId(), input.CurrentPassword, input.NewPassword);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error!);
            return View(new ChangePasswordInput());
        }

        // Sign in again (keeping "keep me signed in") so the "default password" warning disappears.
        if (await users.GetAsync(User.UserId()) is { } me)
        {
            var current = await HttpContext.AuthenticateAsync(PortalAuth.Scheme);
            await HttpContext.SignInAsync(PortalAuth.Scheme, PortalAuth.CreatePrincipal(me), current.Properties);
        }
        Toast("Your password has been changed.");
        return RedirectToAction("Index", "Dashboard");
    }
}
