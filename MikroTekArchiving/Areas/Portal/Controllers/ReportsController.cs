using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

[Permission(Permissions.ReportsView)]
public class ReportsController(ICashAdvances advances, IDisbursements vouchers, ISchoolProfile school) : PortalControllerBase
{
    public async Task<IActionResult> Index() => View(new ReportsPage
    {
        CashAdvances = await advances.GetAllAsync(),
        Vouchers = (await vouchers.GetAllAsync()).OrderByDescending(v => v.Date).ThenByDescending(v => v.DvNo).ToList()
    });

    /// <summary>Cash Disbursements Register (Appendix 43) for one cash advance.</summary>
    public Task<IActionResult> Register(long id) => ReportAsync(id);

    /// <summary>Alphabetical List of Payees from whom taxes were withheld.</summary>
    public Task<IActionResult> Alphalist(long id) => ReportAsync(id);

    private async Task<IActionResult> ReportAsync(long id)
    {
        var ca = await advances.GetAsync(id);
        if (ca is null)
        {
            ToastError("Please choose a cash advance for the report.");
            return RedirectToAction(nameof(Index));
        }
        ViewData["School"] = await school.GetAsync();
        return View(ca);
    }
}
