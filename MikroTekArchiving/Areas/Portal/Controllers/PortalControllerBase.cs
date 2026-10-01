using Microsoft.AspNetCore.Mvc;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>
/// Base for every page in the Disbursement Archiving Portal (/Portal/...).
/// TEMPLATE STAGE: pages show sample data and forms do not save yet.
/// Later: add [Authorize] here and call the services from MikroTek.Services.
/// </summary>
[Area("Portal")]
public abstract class PortalControllerBase : Controller
{
    /// <summary>Used by the template forms: shows a notice instead of saving.</summary>
    protected IActionResult TemplateSaved(string what, string action, object? routeValues = null)
    {
        TempData["Toast"] = $"{what} — template only, nothing was saved yet.";
        return RedirectToAction(action, routeValues);
    }
}
