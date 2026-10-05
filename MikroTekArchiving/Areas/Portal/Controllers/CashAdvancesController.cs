using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.TemplateData;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

[Permission(Permissions.CashAdvancesView)]
public class CashAdvancesController : PortalControllerBase
{
    public IActionResult Index() => View(SampleData.CashAdvances);

    public IActionResult Details(int id) => View(SampleData.CashAdvance(id));

    [HttpGet, Permission(Permissions.CashAdvancesEncode)]
    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken, ActionName("Create"), Permission(Permissions.CashAdvancesEncode)]
    public IActionResult CreatePost() => TemplateSaved("Cash advance recorded", nameof(Index));
}
