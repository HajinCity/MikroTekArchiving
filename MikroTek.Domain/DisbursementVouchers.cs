using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>
    /// A Disbursement Voucher (Appendix 32) in the archive.
    /// (The hotspot internet codes are the separate "Vouchers" table.)
    /// </summary>
    public class DisbursementVouchers : AuditableEntity
    {
        public long CashAdvanceId { get; set; }
        public CashAdvances? CashAdvance { get; set; }

        /// <summary>
        /// true = created by "Upload Scanned DV" and not yet encoded. Drafts are not counted
        /// in totals, balances or reports. Empty until encoded: DvNo.
        /// </summary>
        public bool IsDraft { get; set; }

        /// <summary>"JHS2026-03-053". Empty (null) only while it is a draft.</summary>
        public string? DvNo { get; set; }
        public DateTime Date { get; set; }

        /// <summary>The transaction type = the name of the checklist template used (kept even if the template changes).</summary>
        public string? Type { get; set; }
        public long? ChecklistTemplateId { get; set; }

        public string? Payee { get; set; }
        public string? Tin { get; set; }
        public string? Address { get; set; }
        public string? Particulars { get; set; }
        public string? FundCluster { get; set; }
        public string? ResponsibilityCenter { get; set; }
        public string? MfoPap { get; set; }
        public string? OrsBurs { get; set; }

        /// <summary>Withholding tax 1% and 3% (amounts, not rates).</summary>
        public decimal Tax1 { get; set; }
        public decimal Tax3 { get; set; }

        public string? Mode { get; set; }
        public string? CheckNo { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? Bank { get; set; }
        public string? ReceivedBy { get; set; }
        public string? OrNo { get; set; }
        public string? JevNo { get; set; }

        public string? ChecklistRemarks { get; set; }

        public List<DvLines> Lines { get; set; } = [];
        public List<DvChecklistItems> Checklist { get; set; } = [];
    }

    /// <summary>One debit line of a DV's accounting entry (an expense account and its amount).</summary>
    public class DvLines : BaseEntity
    {
        public long DvId { get; set; }
        public DisbursementVouchers? Dv { get; set; }
        public int SortOrder { get; set; }
        public string Code { get; set; } = string.Empty;
        /// <summary>The account title at the time of encoding.</summary>
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    /// <summary>One supporting document on a DV's checklist.</summary>
    public class DvChecklistItems : BaseEntity
    {
        public long DvId { get; set; }
        public DisbursementVouchers? Dv { get; set; }
        public int SortOrder { get; set; }
        public string Label { get; set; } = string.Empty;
        public ChecklistMark Mark { get; set; }
        /// <summary>The date written beside the item on the cover sheet, as typed ("08/13").</summary>
        public string? DateText { get; set; }
        public string? Remarks { get; set; }
    }
}
