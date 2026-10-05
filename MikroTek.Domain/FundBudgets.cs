using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>
    /// The budget of one fund for one year, e.g. 2026 · JHS · Annual Budget = 1,200,000.00.
    /// One row per year + department + fund type. Typed in by the Admin (Setup → Fund Budgets).
    /// </summary>
    public class FundBudgets : AuditableEntity
    {
        public int Year { get; set; }
        public Department Department { get; set; }
        public FundType FundType { get; set; }
        public decimal Amount { get; set; }
    }
}
