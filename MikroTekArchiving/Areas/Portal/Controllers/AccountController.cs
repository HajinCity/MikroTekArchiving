using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.Interface;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Staff sign-in and sign-out. (Editing your own account is in SettingsController.)</summary>
public class AccountController(IUsers users) : PortalControllerBase
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectHome();
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

        if (result.UsingDefaultPassword || result.User!.MustChangePassword)
        {
            ToastError(result.UsingDefaultPassword
                ? "You signed in with the default admin password. Please change it now."
                : "You signed in with a temporary password. Please choose your own password now.");
            return RedirectToAction("Account", "Settings");
        }
        // "Home" resolves to the first page this person may open (their access is not loaded yet in this request).
        return Url.IsLocalUrl(input.ReturnUrl) ? Redirect(input.ReturnUrl!) : RedirectToAction(nameof(Home));
    }

    /// <summary>Sends the signed-in person to the first page they are allowed to open.</summary>
    [HttpGet]
    public IActionResult Home() => RedirectHome();

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

    /// <summary>Old address of the change-password page: it now lives in Settings → My Account.</summary>
    [HttpGet]
    public IActionResult ChangePassword() => RedirectToAction("Account", "Settings");
}
