using Microsoft.AspNetCore.Mvc;
using MikroTekArchiving.Models.ViewModels;
using MikroTekArchiving.Services;
using MikroTekArchiving.Services.Routers;

namespace MikroTekArchiving.Controllers;

/// <summary>
/// The user side of the hotspot. These actions replace the MikroTik pages:
///   login.html  -> Login      alogin.html -> LoggedIn
///   status.html -> Status     logout.html -> Logout
/// </summary>
public class HotspotController(VoucherService vouchers, IRouterService router) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Login(string? reason)
    {
        var client = router.GetClient(HttpContext);
        if (await vouchers.GetStatusAsync(client) is not null)
            return RedirectToAction(nameof(Status)); // already online

        var model = new HotspotLoginViewModel
        {
            Info = reason == "expired" ? "Your time is up. Please enter a new voucher code." : null
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(HotspotLoginViewModel model)
    {
        var client = router.GetClient(HttpContext);
        var result = await vouchers.LoginAsync(model.Code, client);

        if (!result.Success)
        {
            model.Error = result.Error;
            return View(model);
        }
        return RedirectToAction(nameof(LoggedIn));
    }

    /// <summary>Short "logging you in..." screen, then on to Status (like alogin.html).</summary>
    [HttpGet]
    public IActionResult LoggedIn() => View();

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var status = await vouchers.GetStatusAsync(router.GetClient(HttpContext));
        if (status is null) return RedirectToAction(nameof(Login));
        return View(status);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var summary = await vouchers.LogoutAsync(router.GetClient(HttpContext));
        if (summary is null) return RedirectToAction(nameof(Login));
        return View(summary);
    }
}
