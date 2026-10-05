using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>A MOOE cash advance released to the school (School MOOE Slip). Every DV is charged to one.</summary>
    public class CashAdvances : AuditableEntity
    {
        public Department Department { get; set; }
        /// <summary>Which of the department's budgets it is charged to.</summary>
        public FundType FundType { get; set; }

        /// <summary>Used in the DV numbers: JHS2026-<b>03</b>-051.</summary>
        public int BatchNo { get; set; }
        public string PayrollNo { get; set; } = string.Empty;
        public string? AdaNo { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        /// <summary>"July – August 2026".</summary>
        public string? Period { get; set; }
        public string? FundCluster { get; set; }

        public string? Officer { get; set; }
        public string? OfficerPosition { get; set; }
        public string? VerifiedBy { get; set; }
        public string? VerifiedByPosition { get; set; }

        public CashAdvanceStatus Status { get; set; } = CashAdvanceStatus.Open;

        public List<DisbursementVouchers> Vouchers { get; set; } = [];
    }
}
