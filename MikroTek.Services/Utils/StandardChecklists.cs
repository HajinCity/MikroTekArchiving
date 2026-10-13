namespace MikroTek.Services.Utils
{
    /// <summary>
    /// The school's standard cover-sheet checklists (from the bookkeeper's cover sheets). They can be put back any time
    /// from Setup → Checklist Templates → "Restore standard checklists".
    /// </summary>
    public static class StandardChecklists
    {
        public static readonly IReadOnlyList<(string Name, string[] Items)> All =
        [
            ("Job Order",
            [
                "Disbursement Voucher (for reimbursement)", "Timebook and Payroll", "Summary of Payrolls",
                "Authority to render Overtime (if applicable)", "Accomplishment Report", "Daily Time Record (DTR)",
                "Job Order/Contract"
            ]),
            ("Office, Teacher and Pupil Supplies procured outside PS-DBM",
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
            ("Repair and Maintenance (materials for buildings)",
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
            ("Utilities (Electricity / Internet / Water)",
                ["Disbursement Voucher", "Statement of Account / Billing", "Official Receipt", "Certification of Usage"]),
            ("Traveling / Transportation Expenses",
                ["Disbursement Voucher", "Travel Order", "Itinerary of Travel", "Certificate of Travel Completed",
                 "Certificate of Appearance", "Tickets / Receipts"])
        ];
    }
}
