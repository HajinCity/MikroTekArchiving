using Microsoft.AspNetCore.Mvc;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

public class AccountController : PortalControllerBase
{
    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Login(string username, string password) =>
        RedirectToAction("Index", "Dashboard"); // template: no real sign-in yet

    public IActionResult Logout() => RedirectToAction(nameof(Login));
}
