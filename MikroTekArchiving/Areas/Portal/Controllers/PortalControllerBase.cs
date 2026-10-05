using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>
/// Base for every page in the Disbursement Archiving Portal (/Portal/...).
/// [Authorize]: only signed-in staff can open portal pages. Pages that need a
/// specific role add [Authorize(Roles = PortalRoles.Admin)] or PortalRoles.Encoders.
/// </summary>
[Area("Portal")]
[Authorize]
public abstract class PortalControllerBase : Controller
{
    /// <summary>Used by the template forms: shows a notice instead of saving.</summary>
    protected IActionResult TemplateSaved(string what, string action, object? routeValues = null)
    {
        TempData["Toast"] = $"{what} — template only, nothing was saved yet.";
        return RedirectToAction(action, routeValues);
    }

    protected void Toast(string message) => TempData["Toast"] = message;
    protected void ToastError(string message) => TempData["ToastError"] = message;
}
