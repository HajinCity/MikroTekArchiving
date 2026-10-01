using Microsoft.AspNetCore.Mvc;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

public class DashboardController : PortalControllerBase
{
    public IActionResult Index() => View();
}
