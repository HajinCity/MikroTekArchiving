// ─────────────────────────────────────────────────────────────────────────────
// TEMPLATE ONLY — sample data so the front-end design can be clicked through
// before the database exists. Taken from the sample JHS/SHS documents.
// When we connect the real services, the controllers will use DTOs from
// MikroTek.Services instead and this file will be deleted.
// ─────────────────────────────────────────────────────────────────────────────
namespace MikroTek.Archiving.Areas.Portal.TemplateData;

public record CashAdvanceRow(int Id, string Dept, int BatchNo, string PayrollNo, string AdaNo,
    DateTime Date, decimal Amount, string Period, string Officer, string Status,
    MikroTek.Domain.FundType Fund = MikroTek.Domain.FundType.AnnualBudget)
{
    /// <summary>"Annual Budget" or "Special Funds".</summary>
    public string FundName => Fund == MikroTek.Domain.FundType.AnnualBudget ? "Annual Budget" : "Special Funds";
    public List<DvRow> Vouchers => SampleData.Vouchers.Where(v => v.CashAdvanceId == Id).ToList();
    public decimal Spent => Vouchers.Sum(v => v.Net);
    public decimal Balance => Amount - Spent;
    public int PercentUsed => Amount == 0 ? 0 : (int)Math.Round(Spent / Amount * 100);
}

public record ExpenseLine(string Code, string Title, decimal Amount);
public record ChecklistEntry(string Label, string Mark, string? Date = null, string? Remarks = null); // Mark: "✓", "NA", ""

public record DvRow(int Id, string DvNo, DateTime Date, string Dept, int CashAdvanceId, string Type,
    string Payee, string Tin, string Address, string Particulars, List<ExpenseLine> Lines,
    decimal Tax1, decimal Tax3, string Mode, string CheckNo, DateTime? PaidDate, string ReceivedBy,
    List<ChecklistEntry> Checklist, int Attachments)
{
    public decimal Gross => Lines.Sum(l => l.Amount);
    public decimal Taxes => Tax1 + Tax3;
    public decimal Net => Gross - Taxes;
    public int DocsDone => Checklist.Count(c => c.Mark != "");
    public int DocsMissing => Checklist.Count(c => c.Mark == "");
    public bool IsComplete => DocsMissing == 0 && Attachments > 0;
}

/// <summary>Job Order payroll line: who worked, rate, and which days of the month.</summary>
public record LaborRow(string Name, string Occupation, decimal DailyRate, int Year, int Month, int[] Days)
{
    public decimal Amount => DailyRate * Days.Length;
}

/// <summary>One field the scan reader filled in. Status: "read" (found on the scan), "suggested" (guessed, please verify), "missing" (type it in).</summary>
public record ScanField(string Label, string? Value, string Status, int? Page = null, string? Note = null);

/// <summary>What the scan reader decided a page is.</summary>
public record ScanPage(int Page, string Kind, string? ChecklistItem, string Image);

public record ScanResult(int DraftId, string FileName, int Pages, DateTime UploadedAt, string UploadedBy,
    string DetectedType, List<ScanPage> PageMap, Dictionary<string, ScanField> Fields, List<ExpenseLine> Lines);

public record PayeeRow(string Name, string Tin, string Address, string Kind, string TaxType, int DvCount);
public record UacsRow(string Code, string Title, string Group);
public record ChecklistTemplateRow(int Id, string Name, bool BuiltIn, List<string> Items);
public record UserRow(string Username, string FullName, string Position, string Role, DateTime? LastLogin, bool Active);

public static class SampleData
{
    public const string School = "LEONARDO NATIONAL HIGH SCHOOL";

    public static readonly List<UacsRow> Accounts =
    [
        new("1990101000", "Advances for Operating Expenses", "Asset"),
        new("1040502000", "Semi-Expendable Office Equipment (Inventory)", "Asset"),
        new("2020101000", "Due to BIR", "Liability"),
        new("5020101000", "Traveling Expenses - Local", "Expense"),
        new("5020301000", "Office Supplies Expenses", "Expense"),
        new("5020321002", "Semi-Expendable Office Equipment", "Expense"),
        new("5020321003", "Semi-Expendable ICT Equipment", "Expense"),
        new("5020321008", "Semi-Expendable DRR Equipment", "Expense"),
        new("5020399000", "Other Supplies and Materials Expenses", "Expense"),
        new("5020402000", "Electricity Expenses", "Expense"),
        new("5020503000", "Internet Subscription Expenses", "Expense"),
        new("5021299000", "Other General Services", "Expense"),
        new("5021304002", "Repair and Maintenance - School Buildings", "Expense"),
        new("5029904000", "Transportation and Delivery Expenses", "Expense"),
    ];

    static ExpenseLine L(string code, decimal amt) => new(code, Accounts.First(a => a.Code == code).Title, amt);

    public static readonly List<ChecklistTemplateRow> Templates =
    [
        new(1, "Job Order", true,
        [
            "Disbursement Voucher (for reimbursement)", "Timebook and Payroll", "Summary of Payrolls",
            "Authority to render Overtime (if applicable)", "Accomplishment Report", "Daily Time Record (DTR)",
            "Job Order/Contract"
        ]),
        new(2, "Office, Teacher and Pupil Supplies procured outside PS-DBM", true,
        [
            "Disbursement Voucher", "Official/Collection Receipt/Cash Sales Invoice", "Distribution List",
            "Report of Supplies and Materials Issued (RSMI)", "Requisition and Issue Slip (RIS)",
            "Inventory Custodian Slip (ICS) for semi-expendable items (P15,000 below)", "Warranty Card (if applicable)",
            "Photos", "Inspection and Acceptance Report (IAR)", "Delivery Receipt/Charge Sales Invoice", "Purchase Order",
            "Notice of Award", "BAC Resolution (Procurement and Award)", "Abstract of Quotation", "RFQ Distribution Record",
            "Request for Quotation (RFQ) for at least three (3) bidders/suppliers", "Certificate of Posting",
            "PHILGEPS Certification of the Supplier (for new supplier)", "PHILGEPS Posting (Php50,000.00 and above ABC)",
            "Certificate of Non-Availability of Stocks (CNAS) from PS-DBM", "Agency Procurement Request (APR)",
            "Purchase Request", "Memorandum, if for trainings or seminars",
            "Training Matrix (with the budgetary requirements), if for trainings or seminars"
        ]),
        new(3, "Repair and Maintenance (materials for buildings)", true,
        [
            "Official/Collection Receipt/Sales Invoice", "Disbursement Voucher",
            "Report of Supplies and Materials Issued (RSMI) if already issued",
            "Inventory Custodian Slip (ICS) for semi-expendable items (P15,000 below)", "Accomplishment Report",
            "District TWG Inspection/Monitoring Report", "Photos - Actual Materials", "Photos - Before", "Photos - During",
            "Photos - After", "Inspection and Acceptance Report (IAR)", "Delivery Receipt/Charge Sales Invoice",
            "Purchase Order", "Notice of Award", "BAC Resolution (Procurement and Award)", "Abstract of Quotation",
            "RFQ Distribution Record", "Request for Quotation (RFQ) for at least three (3) bidders/suppliers",
            "Certificate of Posting", "PHILGEPS Posting (Php50,000.00 and above ABC)", "Purchase Request",
            "Pre and Post-Repair Inspection Report", "Waste Materials Report for replacement", "Program of Works"
        ]),
        new(4, "Utilities (Electricity / Internet / Water)", false,
            ["Disbursement Voucher", "Statement of Account / Billing", "Official Receipt", "Certification of Usage"]),
        new(5, "Traveling / Transportation Expenses", false,
            ["Disbursement Voucher", "Travel Order", "Itinerary of Travel", "Certificate of Travel Completed",
             "Certificate of Appearance", "Tickets / Receipts"]),
    ];

    // Build a checklist from a template, marking all as done except the listed ones.
    static List<ChecklistEntry> CL(int templateId, string date, params string[] missing) =>
        Templates.First(t => t.Id == templateId).Items
            .Select(i => missing.Contains(i) ? new ChecklistEntry(i, "")
                       : i.Contains("if applicable") || i.Contains("Notice of Award") || i.Contains("PHILGEPS")
                         || i.Contains("trainings") ? new ChecklistEntry(i, "NA")
                       : new ChecklistEntry(i, "✓", date))
            .ToList();

    public static readonly List<CashAdvanceRow> CashAdvances =
    [
        new(1, "SHS", 3, "OE-26-07-2118", "2026070077", new(2026, 7, 29), 229_900.00m, "July – August 2026", "Jessiebel M. Cana", "Liquidated"),
        new(2, "JHS", 3, "OE-26-07-2117", "2026070076", new(2026, 7, 29), 250_000.00m, "July – August 2026", "Jessiebel M. Cana", "Open"),
    ];

    public static readonly List<DvRow> Vouchers =
    [
        // Senior High (from the Cash Disbursements Register sample)
        new(1, "SHS2026-03-037", new(2026, 8, 5), "SHS", 1, "Utilities (Electricity / Internet / Water)",
            "GUILLIES OFFICE SUPPLIES AND GEN. MDSE. CO. LTD", "626-341-557-000", "Pagadian City, Zamboanga del Sur",
            "To payment of internet subscription for the month of July 2026", [L("5020503000", 3_992.00m)],
            39.92m, 119.76m, "Check", "0103264037", new(2026, 8, 5), "Neljane B. Pradia", CL(4, "08/05"), 1),
        new(2, "SHS2026-03-038", new(2026, 8, 12), "SHS", 1, "Office, Teacher and Pupil Supplies procured outside PS-DBM",
            "GUILLIES OFFICE SUPPLIES AND GEN. MDSE. CO. LTD", "626-341-557-000", "Pagadian City, Zamboanga del Sur",
            "To payment of purchased office supplies and semi-expendable ICT equipment",
            [L("5020301000", 196.00m), L("5020321003", 39_682.00m)],
            398.78m, 1_196.34m, "Check", "0103264038", new(2026, 8, 12), "Neljane B. Pradia", CL(2, "08/11"), 1),
        new(3, "SHS2026-03-039", new(2026, 8, 14), "SHS", 1, "Office, Teacher and Pupil Supplies procured outside PS-DBM",
            "MK ENTERPRISES", "416-950-272-000", "Josefina, Zamboanga del Sur",
            "To payment of purchased office supplies, other supplies and semi-expendable office equipment",
            [L("5020301000", 159_183.00m), L("5020399000", 6_626.00m), L("5020321002", 9_225.00m)],
            1_750.34m, 5_251.02m, "Check", "0103264039", new(2026, 8, 14), "MK Enterprises", CL(2, "08/13"), 1),
        new(4, "SHS2026-03-040", new(2026, 8, 14), "SHS", 1, "Utilities (Electricity / Internet / Water)",
            "JESSIEBEL M. CANA", "Employee", "Josefina, Zamboanga del Sur",
            "To reimbursement of electricity expenses", [L("5020402000", 7_199.64m)],
            0, 0, "Check", "0103264040", new(2026, 8, 14), "Jessiebel M. Cana", CL(4, "08/14"), 1),
        new(5, "SHS2026-03-041", new(2026, 9, 2), "SHS", 1, "Office, Teacher and Pupil Supplies procured outside PS-DBM",
            "MK ENTERPRISES", "416-950-272-000", "Josefina, Zamboanga del Sur",
            "To payment of office supplies, ICT, DRR equipment and other supplies",
            [L("5020301000", 3_612.00m), L("5020321003", 1_284.00m), L("5020399000", 198.00m), L("5020321008", 1_998.00m)],
            70.92m, 212.76m, "Check", "0103264041", new(2026, 9, 2), "MK Enterprises", CL(2, "09/01"), 1),
        new(6, "SHS2026-03-042", new(2026, 9, 2), "SHS", 1, "Traveling / Transportation Expenses",
            "JENESA O. LIMPAHAN", "Employee", "Josefina, Zamboanga del Sur",
            "To reimbursement of traveling and transportation expenses",
            [L("5020101000", 3_148.20m), L("5029904000", 800.00m)],
            0, 0, "Check", "0103264042", new(2026, 9, 2), "Jenesa O. Limpahan",
            CL(5, "09/02", "Certificate of Appearance"), 1),
        new(7, "SHS2026-03-043", new(2026, 9, 2), "SHS", 1, "Traveling / Transportation Expenses",
            "HAZEL M. LLAGAS", "Employee", "Josefina, Zamboanga del Sur",
            "To reimbursement of traveling expenses", [L("5020101000", 1_796.00m)],
            0, 0, "Check", "0103264043", new(2026, 9, 2), "Hazel M. Llagas", CL(5, "09/02"), 1),

        // Junior High (from the JHS2026-03-051/052/053 packets)
        new(8, "JHS2026-03-051", new(2026, 8, 4), "JHS", 2, "Repair and Maintenance (materials for buildings)",
            "GUILLIES OFFICE SUPPLIES AND GENERAL MDSE. CO. LTD", "626-341-557-000", "Pagadian City, Zamboanga del Sur",
            "To payment for the Construction Supplies use for Enhancement of Computer Room, et.al",
            [L("5021304002", 141_960.00m)], 1_419.60m, 4_258.80m, "Check", "0095416383", new(2026, 8, 4), "Neljane B. Pradia",
            CL(3, "07/31", "Official/Collection Receipt/Sales Invoice", "Accomplishment Report",
               "District TWG Inspection/Monitoring Report", "Photos - Before", "Photos - During", "Photos - After"), 1),
        new(9, "JHS2026-03-052", new(2026, 8, 11), "JHS", 2, "Office, Teacher and Pupil Supplies procured outside PS-DBM",
            "MK ENTERPRISES", "416-950-272-000", "Josefina, Zamboanga del Sur",
            "To Payment of the Purchased Office Supplies and Semi Expendable Office Equipment of Leonardo National High School - Junior HS Department",
            [L("5020301000", 44_806.50m), L("5020321002", 22_866.00m)], 676.72m, 2_030.18m, "Check", "0095416384",
            new(2026, 8, 12), "MK Enterprises",
            CL(2, "08/12", "Official/Collection Receipt/Cash Sales Invoice", "Distribution List",
               "Report of Supplies and Materials Issued (RSMI)", "Requisition and Issue Slip (RIS)", "Photos"), 1),
        new(10, "JHS2026-03-053", new(2026, 8, 13), "JHS", 2, "Job Order",
            "JUVY CARLO PORNIA", "N/A", "Josefina, Zamboanga del Sur",
            "To payment of labor for the Enhancement of School Clinic Roof & Classroom Aquarius Windows",
            [L("5021299000", 5_400.00m)], 0, 0, "Check", "0095416385", new(2026, 8, 13), "Juvy Carlo Pornia",
            CL(1, "08/13"), 1),
    ];

    public static readonly List<PayeeRow> Payees =
    [
        new("GUILLIES OFFICE SUPPLIES AND GEN. MDSE. CO. LTD", "626-341-557-000", "Pagadian City, Zamboanga del Sur", "Supplier", "Non-VAT", 3),
        new("MK ENTERPRISES", "416-950-272-000", "Josefina, Zamboanga del Sur", "Supplier", "Non-VAT", 3),
        new("JUVY CARLO PORNIA", "N/A", "Josefina, Zamboanga del Sur", "Job Order Worker", "Exempt", 1),
        new("JESSIEBEL M. CANA", "Employee", "Josefina, Zamboanga del Sur", "Employee", "Exempt", 1),
        new("JENESA O. LIMPAHAN", "Employee", "Josefina, Zamboanga del Sur", "Employee", "Exempt", 1),
        new("HAZEL M. LLAGAS", "Employee", "Josefina, Zamboanga del Sur", "Employee", "Exempt", 1),
    ];

    public static readonly List<UserRow> Users =
    [
        new("admin", "System Administrator", "ICT Coordinator", "Admin", new(2026, 9, 30, 8, 12, 0), true),
        new("avillegas", "Anely R. Villegas", "ADAS III / Senior Bookkeeper", "Bookkeeper", new(2026, 9, 30, 7, 55, 0), true),
        new("jcana", "Jessiebel M. Cana", "School Principal II", "Viewer", new(2026, 9, 29, 16, 3, 0), true),
        new("rquiroga", "Riza P. Quiroga", "Validator / ADAS III (Division)", "Viewer", null, false),
    ];

    /// <summary>Timebook &amp; payroll per Job Order DV (from JHS2026-03-053).</summary>
    public static readonly Dictionary<int, List<LaborRow>> Labor = new()
    {
        [10] = [new("JUVY CARLO PORNIA", "Carpenter/Welder", 600m, 2026, 8, [3, 4, 5, 6, 7, 10, 11, 12, 13])],
    };

    /// <summary>
    /// What the offline OCR actually managed to read from JHS2026-03-053 (tested on the real scan).
    /// Handwritten parts (DV no., dates, check no.) come back as "missing" for the bookkeeper to type.
    /// </summary>
    public static readonly ScanResult Scan = new(
        DraftId: 101, FileName: "JHS2026-03-053.pdf", Pages: 10, UploadedAt: new(2026, 9, 30, 9, 14, 0), UploadedBy: "avillegas",
        DetectedType: "Job Order",
        PageMap:
        [
            new(1, "Checklist cover sheet — \"JOB ORDER\"", null, "01"),
            new(2, "Disbursement Voucher (Appendix 32)", "Disbursement Voucher (for reimbursement)", "02"),
            new(3, "Check — DBP", null, "03"),
            new(4, "Time Book and Payroll", "Timebook and Payroll", "04"),
            new(5, "Summary of Payrolls", "Summary of Payrolls", "05"),
            new(6, "Accomplishment Report", "Accomplishment Report", "06"),
            new(7, "Accomplishment Report", "Accomplishment Report", "07"),
            new(8, "Daily Time Record (CS Form 48)", "Daily Time Record (DTR)", "08"),
            new(9, "Job Order", "Job Order/Contract", "09"),
            new(10, "Job Order", "Job Order/Contract", "10"),
        ],
        Fields: new()
        {
            ["DvNo"] = new("DV No.", null, "missing", 2, "Handwritten — type it from the top-right box"),
            ["Date"] = new("DV Date", null, "missing", 2, "Handwritten"),
            ["FundCluster"] = new("Fund Cluster", "Regular", "read", 2),
            ["Payee"] = new("Payee", "JUVY CARLO PORNIA", "suggested", 2, "Taken from the \"Printed Name\" in Receipt of Payment"),
            ["Tin"] = new("TIN / Employee No.", "N/A", "suggested", 2),
            ["Address"] = new("Address", "Josefina, Zamboanga del Sur", "read", 2),
            ["Particulars"] = new("Particulars", "To payment of labor for the Enhancement of School Clinic Roof & Classroom Aquarius Windows as per supporting papers hereto attached", "read", 2),
            ["Rc"] = new("Responsibility Center", "1000-10000", "read", 2),
            ["MfoPap"] = new("MFO/PAP", "MOOE", "read", 2),
            ["Amount"] = new("Amount Due", "5400.00", "read", 2, "Matches \"Five Thousand Four Hundred Pesos Only\""),
            ["Mode"] = new("Mode of payment", "Check", "read", 2),
            ["CheckNo"] = new("Check / ADA No.", null, "missing", 2, "Handwritten — see also the check on page 3"),
            ["PaidDate"] = new("Date paid", null, "missing", 2, "Handwritten"),
            ["Bank"] = new("Bank / Account", "DBP Pagadian 0-06071840-8", "read", 2),
            ["ReceivedBy"] = new("Received by", "JUVY CARLO PORNIA", "read", 2),
        },
        Lines: [L("5021299000", 5_400.00m)]);

    public static CashAdvanceRow CashAdvance(int id) => CashAdvances.FirstOrDefault(c => c.Id == id) ?? CashAdvances[0];
    public static DvRow Voucher(int id) => Vouchers.FirstOrDefault(v => v.Id == id) ?? Vouchers[^1];
}
