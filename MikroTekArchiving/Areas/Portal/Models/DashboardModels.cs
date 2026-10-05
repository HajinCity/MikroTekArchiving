using MikroTek.Services.DTO;
using MikroTek.Domain;

namespace MikroTek.Archiving.Areas.Portal.Models;

/// <summary>What the dashboard is filtered by: a date range (DV date) and optionally one department.</summary>
public class DashboardFilter
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    /// <summary>"JHS", "SHS" or null for all departments.</summary>
    public string? Dept { get; init; }

    /// <summary>Budgets are per year: the cards use the year of the "To" date.</summary>
    public int BudgetYear => To.Year;
    public string DeptText => Dept is null ? "All departments" : Dept == "JHS" ? "Junior High (JHS)" : "Senior High (SHS)";
    public string RangeText => From.Year == To.Year ? $"{From:MMM d} – {To:MMM d, yyyy}" : $"{From:MMM d, yyyy} – {To:MMM d, yyyy}";

    public bool Includes(DateTime date) => date.Date >= From && date.Date <= To;
    public bool IncludesDept(string dept) => Dept is null || Dept == dept;

    /// <summary>Missing or odd input falls back to safe values: this year, all departments.</summary>
    public static DashboardFilter Create(DateTime? from, DateTime? to, string? dept, DateTime today)
    {
        var f = (from ?? new DateTime(today.Year, 1, 1)).Date;
        var t = (to ?? new DateTime(from?.Year ?? today.Year, 12, 31)).Date;
        if (f > t) (f, t) = (t, f);
        dept = dept?.Trim().ToUpperInvariant();
        return new DashboardFilter { From = f, To = t, Dept = dept is "JHS" or "SHS" ? dept : null };
    }

    /// <summary>For links that must keep the current filter (Export, presets).</summary>
    public Dictionary<string, string?> Route(DateTime? from = null, DateTime? to = null) => new()
    {
        ["from"] = (from ?? From).ToString("yyyy-MM-dd"),
        ["to"] = (to ?? To).ToString("yyyy-MM-dd"),
        ["dept"] = Dept
    };
}

/// <summary>One of the four fund cards: JHS/SHS × Annual Budget/Special Funds.</summary>
/// <param name="SpentInRange">Net amount of the DVs dated inside the selected dates.</param>
/// <param name="SpentInYear">Net amount of all DVs of the budget year up to the "To" date.</param>
public record FundCard(string Dept, FundType Fund, int Year, decimal Budget, decimal SpentInRange, decimal SpentInYear, int DvCount)
{
    public string FundName => Fund == FundType.AnnualBudget ? "Annual Budget" : "Special Funds";
    public string Name => $"{Dept} {FundName}";
    public bool HasBudget => Budget > 0;
    public decimal Remaining => Budget - SpentInYear;
    public bool OverBudget => HasBudget && Remaining < 0;
    public decimal SpentEarlier => SpentInYear - SpentInRange;
    public decimal PercentUsed => HasBudget ? Math.Round(SpentInYear / Budget * 100, 1) : 0;
}

public record MonthRow(DateTime Month, decimal Jhs, decimal Shs)
{
    public decimal Total => Jhs + Shs;
}

public record AmountRow(string Name, string? Code, decimal Amount);

/// <summary>Everything the dashboard shows for one filter. The Excel export writes exactly this.</summary>
public class DashboardData
{
    public required DashboardFilter Filter { get; init; }
    public required List<DvDto> Vouchers { get; init; }
    public required List<CashAdvanceDto> CashAdvances { get; init; }
    public required List<FundCard> Funds { get; init; }
    public required List<MonthRow> Months { get; init; }
    public required List<AmountRow> ByType { get; init; }
    public required List<AmountRow> ByAccount { get; init; }

    public List<DvDto> Incomplete => Vouchers.Where(v => !v.IsComplete).ToList();
    public decimal Gross => Vouchers.Sum(v => v.Gross);
    public decimal Taxes => Vouchers.Sum(v => v.Taxes);
    public decimal Net => Vouchers.Sum(v => v.Net);
}
