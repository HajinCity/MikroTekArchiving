using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.TemplateData;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

public class ReportsController : PortalControllerBase
{
    public IActionResult Index() => View();

    /// <summary>Cash Disbursements Register (Appendix 43) for one cash advance.</summary>
    public IActionResult Register(int id = 1) => View(SampleData.CashAdvance(id));

    /// <summary>Alphabetical List of Payees from whom taxes were withheld.</summary>
    public IActionResult Alphalist(int id = 1) => View(SampleData.CashAdvance(id));
}
