using Microsoft.AspNetCore.Mvc;
using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Archiving.Areas.Portal.Controllers;

public class DashboardController(IFundBudgets budgets, ICashAdvances advances, IDisbursements vouchers, ISchoolProfile school)
    : PortalControllerBase
{
    /// <summary>Dashboard for a date range (DV date) and department. No values = this year, all departments.</summary>
    [Permission(Permissions.DashboardView)]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? dept) =>
        View(await LoadAsync(from, to, dept));

    /// <summary>The same data as the page, as an Excel workbook.</summary>
    [Permission(Permissions.DashboardExport)]
    public async Task<IActionResult> Export(DateTime? from, DateTime? to, string? dept)
    {
        var data = await LoadAsync(from, to, dept);
        var f = data.Filter;
        var name = $"Dashboard_{f.From:yyyy-MM-dd}_to_{f.To:yyyy-MM-dd}{(f.Dept is null ? "" : "_" + f.Dept)}.xlsx";
        return File(DashboardExcel.Create(data, User.FullName(), (await school.GetAsync()).SchoolName ?? ""),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private async Task<DashboardData> LoadAsync(DateTime? from, DateTime? to, string? dept)
    {
        var filter = DashboardFilter.Create(from, to, dept, DateTime.Today);
        return DashboardBuilder.Build(filter, await budgets.GetForYearAsync(filter.BudgetYear),
            await advances.GetAllAsync(), await vouchers.GetAllAsync());
    }
}
