using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.TemplateData;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

public class CashAdvancesController : PortalControllerBase
{
    public IActionResult Index() => View(SampleData.CashAdvances);

    public IActionResult Details(int id) => View(SampleData.CashAdvance(id));

    [HttpGet, Authorize(Roles = PortalRoles.Encoders)]
    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken, ActionName("Create"), Authorize(Roles = PortalRoles.Encoders)]
    public IActionResult CreatePost() => TemplateSaved("Cash advance recorded", nameof(Index));
}
