using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MikroTek.Archiving.Areas.Portal.Helpers;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>
/// Base for every page in the Disbursement Archiving Portal (/Portal/...).
/// [Authorize]: only signed-in staff can open portal pages. Pages for a specific
/// feature add [Permission(Permissions.Something)] (ticked per role in Settings → Roles &amp; Access).
/// </summary>
[Area("Portal")]
[Authorize]
public abstract class PortalControllerBase : Controller
{
    /// <summary>
    /// Someone signed in with a temporary password (given by an admin) can only open
    /// My Account until they choose their own password.
    /// </summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (User.HasTemporaryPassword() && this is not AccountController
            && !(this is SettingsController && context.RouteData.Values["action"] is "Account" or "SaveAccount" or "ChangePassword"))
        {
            ToastError("Your password is temporary. Please choose your own password to continue.");
            context.Result = RedirectToAction("Account", "Settings");
        }
        base.OnActionExecuting(context);
    }

    protected void Toast(string message) => TempData["Toast"] = message;
    protected void ToastError(string message) => TempData["ToastError"] = message;

    /// <summary>The first page the signed-in person is allowed to open.</summary>
    protected IActionResult RedirectHome()
    {
        var (controller, action) = User.HomePage();
        return RedirectToAction(action, controller);
    }
}
