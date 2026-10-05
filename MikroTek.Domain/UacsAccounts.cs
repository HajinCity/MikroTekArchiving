using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>One line of the chart of accounts (Unified Accounts Code Structure) used on DVs.</summary>
    public class UacsAccounts : AuditableEntity
    {
        /// <summary>The 10-digit UACS object code, e.g. "5020301000".</summary>
        public string Code { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public AccountGroup Group { get; set; } = AccountGroup.Expense;

        /// <summary>false = hidden from the pick lists when encoding (old DVs keep showing it).</summary>
        public bool IsActive { get; set; } = true;
    }
}
