using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    /// <summary>One fund's budget for a year. Amount is 0 when the Admin has not set it yet.</summary>
    public record FundBudgetDto(int Year, Department Department, FundType FundType, decimal Amount)
    {
        public string FundName => FundType == FundType.AnnualBudget ? "Annual Budget" : "Special Funds";
        /// <summary>e.g. "JHS Annual Budget"</summary>
        public string Label => $"{Department} {FundName}";
    }

    /// <summary>The four budgets of one year, as typed on the Setup → Fund Budgets page.</summary>
    public record SaveFundBudgetsRequest(int Year, decimal JhsAnnual, decimal ShsAnnual, decimal JhsSpecial, decimal ShsSpecial);
}
