using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    // What was read from a scanned MOOE packet (MOOE slip + Alphalist + Cash Disbursements Register),
    // shown on the review page to be checked, corrected and then saved as one cash advance with its DVs.
    // The same classes come back from the review form.

    /// <summary>A note about one field: Level "check" = the scan was unclear, "error" = the numbers do not agree.</summary>
    public record ImportFlag(string Field, string Level, string Message);

    public class PacketDraft
    {
        /// <summary>The folder name under uploads/import that holds the packet and this draft until it is saved.</summary>
        public string Token { get; set; } = "";
        public string FileName { get; set; } = "";
        public int PageCount { get; set; }
        /// <summary>What each page was taken to be: "1 MOOE slip", "2 Alphalist", "3 Register"...</summary>
        public List<string> PageKinds { get; set; } = [];
        public PacketCashAdvance CashAdvance { get; set; } = new();
        public List<PacketVoucher> Vouchers { get; set; } = [];
        /// <summary>Totals printed on the register (to compare with what was read).</summary>
        public decimal? RegisterTotalPayments { get; set; }
        /// <summary>Notes about the whole packet (recapitulation and totals checks).</summary>
        public List<string> Notes { get; set; } = [];
        public double Seconds { get; set; }
    }

    public class PacketCashAdvance
    {
        /// <summary>Not 0 when this Payroll No. is already recorded: the DVs are added to that cash advance.</summary>
        public long ExistingId { get; set; }
        public Department Dept { get; set; }
        public FundType Fund { get; set; } = FundType.AnnualBudget;
        public int BatchNo { get; set; } = 1;
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
        public List<ImportFlag> Flags { get; set; } = [];
    }

    public class PacketVoucher
    {
        /// <summary>Unticked on the review page = not saved (for example a DV already in the archive).</summary>
        public bool Include { get; set; } = true;
        public string? DvNo { get; set; }
        public string? CheckNo { get; set; }
        public DateTime? Date { get; set; }
        public string? Payee { get; set; }
        public string? Tin { get; set; }
        public string? Address { get; set; }
        public PayeeTaxType PayeeTaxType { get; set; } = PayeeTaxType.NonVat;
        public List<PacketLine> Lines { get; set; } = [];
        public decimal? TaxBase { get; set; }
        public decimal? Tax1 { get; set; }
        public decimal? Tax3 { get; set; }
        public decimal? Tax5 { get; set; }
        /// <summary>The payment on the register (= gross − taxes when everything was read right).</summary>
        public decimal? Payment { get; set; }
        /// <summary>The cash advance balance after this DV, as on the register.</summary>
        public decimal? Balance { get; set; }
        public List<ImportFlag> Flags { get; set; } = [];

        public decimal Gross => Lines.Sum(l => l.Amount ?? 0);
        public decimal Taxes => (Tax1 ?? 0) + (Tax3 ?? 0) + (Tax5 ?? 0);
        public decimal Net => Gross - Taxes;
        public bool HasErrors => Flags.Any(f => f.Level == "error");
        public bool HasChecks => Flags.Count > 0;
        public ImportFlag? FlagFor(string field) => Flags.FirstOrDefault(f => f.Field == field);
    }

    public class PacketLine
    {
        public string? Code { get; set; }
        public string? Title { get; set; }
        public decimal? Amount { get; set; }
        /// <summary>Why this line needs a look (empty = read cleanly).</summary>
        public string? Flag { get; set; }
    }

    /// <summary>Whether the scan reader (Tesseract) is installed on the server.</summary>
    public record ScanReaderStatus(bool Ready, string Message);

    // ------------------------------------------------------------------ one DV scan → the DV form

    /// <summary>
    /// What was read from an uploaded DV scan, to fill the DV form. Fields are keyed by the form's field ids
    /// (dvNo, dvDate, payee, tin, …). Unsure lists the fields that should be looked at before saving.
    /// </summary>
    public class DvScanResult
    {
        public bool Found { get; set; }
        public string Message { get; set; } = "";
        public int PageCount { get; set; }
        public int? DvPage { get; set; }
        public double Seconds { get; set; }
        public Dictionary<string, string> Fields { get; set; } = [];
        public List<DvScanLine> Lines { get; set; } = [];
        public bool ManualTax { get; set; }
        public decimal? TaxBase { get; set; }
        public decimal? Tax1 { get; set; }
        public decimal? Tax3 { get; set; }
        public decimal? Tax5 { get; set; }
        /// <summary>The net amount on the DV (Amount Due / Approved for Payment), to compare with.</summary>
        public decimal? Net { get; set; }
        public List<string> Unsure { get; set; } = [];
        public List<string> Notes { get; set; } = [];
        /// <summary>The ticks read from the cover sheet for the chosen type's checklist: Label → "✓", "NA" or "" (not read).</summary>
        public List<DvScanMark> Checklist { get; set; } = [];
        /// <summary>What each page was taken to be, for checking a reading that went wrong.</summary>
        public List<string> PageKinds { get; set; } = [];
    }

    public record DvScanMark(string Label, string Mark);

    public record DvScanLine(string? Code, string? Title, decimal? Amount, string? Flag);
}
