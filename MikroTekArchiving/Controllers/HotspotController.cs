using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MikroTek.Services.Interface;
using MikroTekArchiving.Models.ViewModels;
using MikroTekArchiving.Utility;

namespace MikroTekArchiving.Controllers;

/// <summary>
/// The user side of the hotspot. These actions replace the MikroTik pages:
///   login.html  -> Login      alogin.html -> LoggedIn
///   status.html -> Status     logout.html -> Logout
/// The rules themselves are in MikroTek.Services (VoucherServices); this controller only shows pages.
/// </summary>
public class HotspotController(IVouchers vouchers, IConfiguration config) : Controller
{
    /// <summary>When the hotspot demo is switched off (school server), every page here answers "not found".</summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!config.GetValue("HotspotDemo:Enabled", true))
            context.Result = NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? reason)
    {
        var client = HotspotDevice.GetClient(HttpContext);
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
        var client = HotspotDevice.GetClient(HttpContext);
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
        var status = await vouchers.GetStatusAsync(HotspotDevice.GetClient(HttpContext));
        if (status is null) return RedirectToAction(nameof(Login));
        return View(status);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var summary = await vouchers.LogoutAsync(HotspotDevice.GetClient(HttpContext));
        if (summary is null) return RedirectToAction(nameof(Login));
        return View(summary);
    }
}
