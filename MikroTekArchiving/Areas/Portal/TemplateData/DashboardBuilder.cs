using MikroTek.Archiving.Areas.Portal.Helpers;
using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.DTO;

namespace MikroTek.Archiving.Areas.Portal.TemplateData;

/// <summary>
/// Works out the dashboard numbers for a filter. Budgets are real (database);
/// DVs and cash advances still come from SampleData until those tables are built.
/// </summary>
public static class DashboardBuilder
{
    public static DashboardData Build(DashboardFilter filter, List<FundBudgetDto> budgets)
    {
        var advances = SampleData.CashAdvances.Where(c => filter.IncludesDept(c.Dept)).ToList();
        var allDvs = SampleData.Vouchers.Where(v => filter.IncludesDept(v.Dept)).ToList();
        var dvs = allDvs.Where(v => filter.Includes(v.Date)).OrderBy(v => v.Date).ThenBy(v => v.DvNo).ToList();

        // Fund cards: a DV belongs to the fund of the cash advance it is charged to.
        var yearStart = new DateTime(filter.BudgetYear, 1, 1);
        var funds = budgets.Where(b => filter.IncludesDept(b.Department.ToString())).Select(b =>
        {
            var caIds = SampleData.CashAdvances.Where(c => c.Dept == b.Department.ToString() && c.Fund == b.FundType).Select(c => c.Id).ToHashSet();
            var mine = SampleData.Vouchers.Where(v => caIds.Contains(v.CashAdvanceId)).ToList();
            var inRange = mine.Where(v => filter.Includes(v.Date)).ToList();
            var inYear = mine.Where(v => v.Date.Date >= yearStart && v.Date.Date <= filter.To);
            return new FundCard(b.Department.ToString(), b.FundType, b.Year, b.Amount, inRange.Sum(v => v.Net), inYear.Sum(v => v.Net), inRange.Count);
        }).ToList();

        var months = dvs.Select(v => new DateTime(v.Date.Year, v.Date.Month, 1)).Distinct().OrderBy(m => m)
            .Select(m => new MonthRow(m,
                dvs.Where(v => v.Dept == "JHS" && v.Date.Year == m.Year && v.Date.Month == m.Month).Sum(v => v.Net),
                dvs.Where(v => v.Dept == "SHS" && v.Date.Year == m.Year && v.Date.Month == m.Month).Sum(v => v.Net)))
            .ToList();

        // Fixed type order, so every type keeps its colour whatever the filter is.
        var byType = SpendTypes.Order.Select(t => new AmountRow(t, null, dvs.Where(v => SpendTypes.Of(v.Type) == t).Sum(v => v.Gross))).ToList();

        var byAccount = dvs.SelectMany(v => v.Lines).GroupBy(l => (l.Title, l.Code))
            .Select(g => new AmountRow(g.Key.Title, g.Key.Code, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount).ToList();

        return new DashboardData
        {
            Filter = filter, Vouchers = dvs, CashAdvances = advances, Funds = funds,
            Months = months, ByType = byType, ByAccount = byAccount
        };
    }
}
