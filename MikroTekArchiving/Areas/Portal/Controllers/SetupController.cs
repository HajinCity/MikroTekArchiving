using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.TemplateData;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

/// <summary>Reference data and settings: payees, UACS accounts, checklist templates, users, school profile.</summary>
public class SetupController : PortalControllerBase
{
    public IActionResult Payees() => View(SampleData.Payees);
    public IActionResult Accounts() => View(SampleData.Accounts);
    public IActionResult Checklists() => View(SampleData.Templates);
    public IActionResult Checklist(int id) => View(SampleData.Templates.FirstOrDefault(t => t.Id == id) ?? SampleData.Templates[0]);
    public IActionResult Users() => View(SampleData.Users);
    public IActionResult School() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Save(string back) => TemplateSaved("Changes saved", back);
}
