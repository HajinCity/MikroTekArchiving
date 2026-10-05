namespace MikroTek.Domain
{
    /// <summary>Where the money for a cash advance comes from.</summary>
    public enum FundType
    {
        /// <summary>The department's regular MOOE budget for the year.</summary>
        AnnualBudget,
        /// <summary>Any other fund outside the annual budget.</summary>
        SpecialFunds
    }
}
