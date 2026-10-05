using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    // ------------------------------------------------------------------ what the pages read

    /// <summary>One debit line of a DV: an expense account and its amount.</summary>
    public record ExpenseLine(string Code, string Title, decimal Amount);

    /// <summary>One supporting document on a DV. Mark: "✓" attached, "NA" not applicable, "" missing.</summary>
    public record ChecklistEntry(string Label, string Mark, string? Date = null, string? Remarks = null);

    /// <summary>A disbursement voucher as the pages and reports see it.</summary>
    public class DvDto
    {
        public long Id { get; init; }
        public bool IsDraft { get; init; }
        public string DvNo { get; init; } = "";
        public DateTime Date { get; init; }
        /// <summary>"JHS" or "SHS" (from its cash advance).</summary>
        public string Dept { get; init; } = "";
        public long CashAdvanceId { get; init; }
        public string CashAdvanceNo { get; init; } = "";
        public FundType Fund { get; init; }
        /// <summary>Transaction type = the checklist used ("Job Order", ...). Empty if none.</summary>
        public string Type { get; init; } = "";
        public long? TemplateId { get; init; }

        public string Payee { get; init; } = "";
        public string Tin { get; init; } = "";
        public string Address { get; init; } = "";
        public string Particulars { get; init; } = "";
        public string FundCluster { get; init; } = "";
        public string ResponsibilityCenter { get; init; } = "";
        public string MfoPap { get; init; } = "";
        public string OrsBurs { get; init; } = "";

        public List<ExpenseLine> Lines { get; init; } = [];
        public decimal Tax1 { get; init; }
        public decimal Tax3 { get; init; }

        public string Mode { get; init; } = "";
        public string CheckNo { get; init; } = "";
        public DateTime? PaidDate { get; init; }
        public string Bank { get; init; } = "";
        public string ReceivedBy { get; init; } = "";
        public string OrNo { get; init; } = "";
        public string JevNo { get; init; } = "";

        public List<ChecklistEntry> Checklist { get; init; } = [];
        public string ChecklistRemarks { get; init; } = "";
        /// <summary>How many scanned files are attached.</summary>
        public int Attachments { get; init; }

        public string? EncodedBy { get; init; }
        public DateTime EncodedAt { get; init; }
        public string? UpdatedBy { get; init; }
        public DateTime? UpdatedAt { get; init; }

        public decimal Gross => Lines.Sum(l => l.Amount);
        public decimal Taxes => Tax1 + Tax3;
        public decimal Net => Gross - Taxes;
        public int DocsDone => Checklist.Count(c => c.Mark != "");
        public int DocsMissing => Checklist.Count(c => c.Mark == "");
        /// <summary>Encoded, nothing missing on the checklist, and at least one scan attached.</summary>
        public bool IsComplete => !IsDraft && DocsMissing == 0 && Attachments > 0;
        /// <summary>What to call it on screen: the DV number, or "Draft #12" before it has one.</summary>
        public string Label => IsDraft || DvNo.Length == 0 ? $"Draft #{Id}" : DvNo;
        /// <summary>The type for charts and lists; DVs with no checklist are grouped as "Other".</summary>
        public string TypeOrOther => Type.Length == 0 ? "Other" : Type;
    }

    /// <summary>A cash advance with the DVs charged to it (drafts are not included in Vouchers or the amounts).</summary>
    public class CashAdvanceDto
    {
        public long Id { get; init; }
        public string Dept { get; init; } = "";
        public FundType Fund { get; init; }
        public int BatchNo { get; init; }
        public string PayrollNo { get; init; } = "";
        public string AdaNo { get; init; } = "";
        public DateTime Date { get; init; }
        public decimal Amount { get; init; }
        public string Period { get; init; } = "";
        public string FundCluster { get; init; } = "";
        public string Officer { get; init; } = "";
        public string OfficerPosition { get; init; } = "";
        public string VerifiedBy { get; init; } = "";
        public string VerifiedByPosition { get; init; } = "";
        /// <summary>"Open" or "Liquidated".</summary>
        public string Status { get; init; } = "Open";
        public List<DvDto> Vouchers { get; init; } = [];
        public int DraftCount { get; init; }

        public bool IsOpen => Status == "Open";
        public string FundName => Fund == FundType.AnnualBudget ? "Annual Budget" : "Special Funds";
        public string DeptName => Dept == "JHS" ? "Junior High" : "Senior High";
        public decimal Spent => Vouchers.Sum(v => v.Net);
        public decimal Balance => Amount - Spent;
        public int PercentUsed => Amount == 0 ? 0 : (int)Math.Round(Spent / Amount * 100);
        /// <summary>The start of this advance's DV numbers: "JHS2026-03-".</summary>
        public string DvPrefix => $"{Dept}{Date.Year}-{BatchNo:00}-";
    }

    public record PayeeDto(long Id, string Name, string Tin, string Address, PayeeKind Kind, PayeeTaxType TaxType, bool IsActive, int DvCount)
    {
        public string KindText => Kind == PayeeKind.JobOrderWorker ? "Job Order Worker" : Kind.ToString();
        public string TaxTypeText => TaxType == PayeeTaxType.NonVat ? "Non-VAT" : TaxType == PayeeTaxType.Vat ? "VAT" : "Exempt";
    }

    public record ChecklistTemplateDto(long Id, string Name, bool IsActive, List<string> Items, int DvCount);

    /// <summary>Numbers for the bell and the menu badge.</summary>
    public record ArchiveAlerts(int Drafts, int Incomplete);

    /// <summary>School details and signatories (read and saved with the same shape).</summary>
    public class SchoolProfileDto
    {
        public string? SchoolName { get; set; }
        public string? Region { get; set; }
        public string? Division { get; set; }
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? JhsBankAccount { get; set; }
        public string? ShsBankAccount { get; set; }
        public string? ResponsibilityCenter { get; set; }
        public string? FundCluster { get; set; }
        public string? HeadName { get; set; }
        public string? HeadPosition { get; set; }
        public string? BookkeeperName { get; set; }
        public string? BookkeeperPosition { get; set; }
        public string? ValidatorName { get; set; }
        public string? ValidatorPosition { get; set; }
        public string? CashUnitName { get; set; }
        public string? CashUnitPosition { get; set; }

        /// <summary>The bank account printed on a department's DVs.</summary>
        public string BankFor(string dept) => (dept == "SHS" ? ShsBankAccount : JhsBankAccount) ?? "";
    }

    // ------------------------------------------------------------------ what the forms send

    public record SavePayeeRequest(long Id, string? Name, string? Tin, string? Address, PayeeKind Kind, PayeeTaxType TaxType, bool IsActive);

    public record SaveChecklistTemplateRequest(long Id, string? Name, bool IsActive, List<string>? Items);

    public class SaveCashAdvanceRequest
    {
        public long Id { get; set; }
        public Department Dept { get; set; }
        public FundType Fund { get; set; }
        public int BatchNo { get; set; }
        public string? PayrollNo { get; set; }
        public string? AdaNo { get; set; }
        public DateTime? Date { get; set; }
        public decimal? Amount { get; set; }
        public string? Period { get; set; }
        public string? FundCluster { get; set; }
        public string? Officer { get; set; }
        public string? OfficerPosition { get; set; }
        public string? VerifiedBy { get; set; }
        public string? VerifiedByPosition { get; set; }
    }

    public class DvLineInput
    {
        public string? Code { get; set; }
        public decimal? Amount { get; set; }
    }

    public class ChecklistInput
    {
        public string? Label { get; set; }
        /// <summary>"✓", "NA" or empty.</summary>
        public string? Mark { get; set; }
        public string? Date { get; set; }
    }

    /// <summary>The DV encoding form. Type = the Id of the checklist template chosen (empty = no checklist).</summary>
    public class SaveDvRequest
    {
        public long Id { get; set; }
        public long CashAdvanceId { get; set; }
        public string? DvNo { get; set; }
        public DateTime? Date { get; set; }
        public long? Type { get; set; }
        public string? FundCluster { get; set; }
        public string? Payee { get; set; }
        public string? Tin { get; set; }
        public string? OrsBurs { get; set; }
        public string? Address { get; set; }
        public string? Particulars { get; set; }
        public string? ResponsibilityCenter { get; set; }
        public string? MfoPap { get; set; }
        public List<DvLineInput> Lines { get; set; } = [];
        public decimal? Tax1 { get; set; }
        public decimal? Tax3 { get; set; }
        public string? Mode { get; set; }
        public string? CheckNo { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? Bank { get; set; }
        public string? ReceivedBy { get; set; }
        public string? OrNo { get; set; }
        public string? JevNo { get; set; }
        public List<ChecklistInput> Check { get; set; } = [];
        public string? ChecklistRemarks { get; set; }
    }
}
