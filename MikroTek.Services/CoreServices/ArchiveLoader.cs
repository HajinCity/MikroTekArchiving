using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;

namespace MikroTek.Services.CoreServices
{
    /// <summary>
    /// Reads cash advances and DVs from the database and turns them into what the pages use
    /// (DvDto / CashAdvanceDto). Shared by CashAdvanceServices and DisbursementServices so
    /// both always give the same numbers.
    /// </summary>
    public class ArchiveLoader(DisbursementVouchersQueries vouchers, CashAdvancesQueries advances,
                               ArchiveFilesQueries files, UsersQueries users)
    {
        public async Task<List<DvDto>> GetVouchersAsync(long? id = null, long? cashAdvanceId = null, bool includeDrafts = true)
        {
            var rows = await vouchers.GetDetailedAsync(id, cashAdvanceId);
            if (!includeDrafts) rows = rows.Where(v => !v.IsDraft).ToList();

            var fileCounts = await files.CountByOwnerAsync(FileOwner.Voucher);
            var names = await users.GetNamesAsync();
            return rows.Select(v => ToDto(v, fileCounts.GetValueOrDefault(v.Id), names)).ToList();
        }

        public async Task<List<CashAdvanceDto>> GetCashAdvancesAsync(long? id = null)
        {
            List<CashAdvances> rows;
            if (id is null) rows = await advances.GetAllSortedAsync();
            else rows = await advances.SelectByIdAsync(id.Value) is { } one ? [one] : [];
            if (rows.Count == 0) return [];

            var dvs = await GetVouchersAsync(cashAdvanceId: id);
            return rows.Select(c =>
            {
                var mine = dvs.Where(v => v.CashAdvanceId == c.Id).ToList();
                return new CashAdvanceDto
                {
                    Id = c.Id, Dept = c.Department.ToString(), Fund = c.FundType, BatchNo = c.BatchNo,
                    PayrollNo = c.PayrollNo, AdaNo = c.AdaNo ?? "", Date = c.Date, Amount = c.Amount,
                    Period = c.Period ?? "", FundCluster = c.FundCluster ?? "",
                    Officer = c.Officer ?? "", OfficerPosition = c.OfficerPosition ?? "",
                    VerifiedBy = c.VerifiedBy ?? "", VerifiedByPosition = c.VerifiedByPosition ?? "",
                    Status = c.Status.ToString(),
                    Vouchers = mine.Where(v => !v.IsDraft).ToList(),
                    DraftCount = mine.Count(v => v.IsDraft)
                };
            }).ToList();
        }

        private static DvDto ToDto(DisbursementVouchers v, int attachments, Dictionary<long, string> names) => new()
        {
            Id = v.Id, IsDraft = v.IsDraft, DvNo = v.DvNo ?? "", Date = v.Date,
            Dept = v.CashAdvance?.Department.ToString() ?? "", CashAdvanceId = v.CashAdvanceId,
            CashAdvanceNo = v.CashAdvance?.PayrollNo ?? "", Fund = v.CashAdvance?.FundType ?? FundType.AnnualBudget,
            Type = v.Type ?? "", TemplateId = v.ChecklistTemplateId,
            Payee = v.Payee ?? "", Tin = v.Tin ?? "", Address = v.Address ?? "", Particulars = v.Particulars ?? "",
            FundCluster = v.FundCluster ?? "", ResponsibilityCenter = v.ResponsibilityCenter ?? "",
            MfoPap = v.MfoPap ?? "", OrsBurs = v.OrsBurs ?? "",
            Lines = v.Lines.OrderBy(l => l.SortOrder).Select(l => new ExpenseLine(l.Code, l.Title, l.Amount)).ToList(),
            Tax1 = v.Tax1, Tax3 = v.Tax3,
            Mode = v.Mode ?? "", CheckNo = v.CheckNo ?? "", PaidDate = v.PaidDate, Bank = v.Bank ?? "",
            ReceivedBy = v.ReceivedBy ?? "", OrNo = v.OrNo ?? "", JevNo = v.JevNo ?? "",
            Checklist = v.Checklist.OrderBy(c => c.SortOrder)
                .Select(c => new ChecklistEntry(c.Label, MarkText(c.Mark), c.DateText, c.Remarks)).ToList(),
            ChecklistRemarks = v.ChecklistRemarks ?? "",
            Attachments = attachments,
            EncodedBy = v.CreatedBy is { } by ? names.GetValueOrDefault(by) : null, EncodedAt = v.CreatedAt,
            UpdatedBy = v.UpdatedBy is { } up ? names.GetValueOrDefault(up) : null, UpdatedAt = v.UpdatedAt
        };

        public static string MarkText(ChecklistMark mark) =>
            mark == ChecklistMark.Attached ? "✓" : mark == ChecklistMark.NotApplicable ? "NA" : "";

        public static ChecklistMark ParseMark(string? text) =>
            text == "✓" ? ChecklistMark.Attached : text == "NA" ? ChecklistMark.NotApplicable : ChecklistMark.Missing;
    }
}
