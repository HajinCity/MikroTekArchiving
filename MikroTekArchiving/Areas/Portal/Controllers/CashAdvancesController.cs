using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Archiving.Areas.Portal.TemplateData;
using MikroTek.Domain;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

[Permission(Permissions.CashAdvancesView)]
public class CashAdvancesController(IArchiveFiles files) : PortalControllerBase
{
    public IActionResult Index() => View(SampleData.CashAdvances);

    public async Task<IActionResult> Details(int id)
    {
        var ca = SampleData.CashAdvance(id);
        ViewData["Files"] = new FilesPanel(FileOwner.CashAdvance, ca.Id, await files.GetForOwnerAsync(FileOwner.CashAdvance, ca.Id),
            CanEdit: User.Can(Permissions.CashAdvancesEncode), Compact: true);
        return View(ca);
    }

    [HttpGet, Permission(Permissions.CashAdvancesEncode)]
    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken, ActionName("Create"), Permission(Permissions.CashAdvancesEncode)]
    public IActionResult CreatePost() => TemplateSaved("Cash advance recorded", nameof(Index));
}
