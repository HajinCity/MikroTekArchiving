using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>
/// UI Kit gallery: every reusable component with a live example and the code to use it.
/// Keep this page as the reference when building the next bookkeeping system.
/// </summary>
[Authorize(Roles = PortalRoles.Admin)]
public class KitController : PortalControllerBase
{
    public IActionResult Index() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Demo() => TemplateSaved("Confirmed action ran", nameof(Index));
}
