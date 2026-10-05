using MikroTek.Archiving.Areas.Portal.Models;
using MikroTek.Services.DTO;

namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>Works out the dashboard numbers for a filter from the budgets, cash advances and encoded DVs.</summary>
public static class DashboardBuilder
{
    public static DashboardData Build(DashboardFilter filter, List<FundBudgetDto> budgets,
                                      List<CashAdvanceDto> allAdvances, List<DvDto> allVouchers)
    {
        var advances = allAdvances.Where(c => filter.IncludesDept(c.Dept)).ToList();
        var dvs = allVouchers.Where(v => filter.IncludesDept(v.Dept) && filter.Includes(v.Date))
            .OrderBy(v => v.Date).ThenBy(v => v.DvNo).ToList();

        // Fund cards: a DV belongs to the fund of the cash advance it is charged to.
        var yearStart = new DateTime(filter.BudgetYear, 1, 1);
        var funds = budgets.Where(b => filter.IncludesDept(b.Department.ToString())).Select(b =>
        {
            var mine = allVouchers.Where(v => v.Dept == b.Department.ToString() && v.Fund == b.FundType).ToList();
            var inRange = mine.Where(v => filter.Includes(v.Date)).ToList();
            var inYear = mine.Where(v => v.Date.Date >= yearStart && v.Date.Date <= filter.To);
            return new FundCard(b.Department.ToString(), b.FundType, b.Year, b.Amount, inRange.Sum(v => v.Net), inYear.Sum(v => v.Net), inRange.Count);
        }).ToList();

        var months = dvs.Select(v => new DateTime(v.Date.Year, v.Date.Month, 1)).Distinct().OrderBy(m => m)
            .Select(m => new MonthRow(m,
                dvs.Where(v => v.Dept == "JHS" && v.Date.Year == m.Year && v.Date.Month == m.Month).Sum(v => v.Net),
                dvs.Where(v => v.Dept == "SHS" && v.Date.Year == m.Year && v.Date.Month == m.Month).Sum(v => v.Net)))
            .ToList();

        var types = SpendTypes.For(dvs);
        var byType = types.Select(t => new AmountRow(t, null, dvs.Where(v => SpendTypes.Of(v, types) == t).Sum(v => v.Gross))).ToList();

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
