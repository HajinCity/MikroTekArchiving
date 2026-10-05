using System.Text.RegularExpressions;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class DisbursementServices : CoreServices, IDisbursements
    {
        private const decimal MaxAmount = 9_999_999_999.99m;
        private const int MaxLines = 30, MaxChecklist = 100;

        private readonly DisbursementVouchersQueries _vouchers;
        private readonly CashAdvancesQueries _advances;
        private readonly UacsAccountsQueries _accounts;
        private readonly ChecklistTemplatesQueries _templates;
        private readonly PayeesQueries _payees;
        private readonly ArchiveLoader _loader;
        private readonly IArchiveFiles _files;

        public DisbursementServices(DB db, Logger logger, DisbursementVouchersQueries vouchers, CashAdvancesQueries advances,
            UacsAccountsQueries accounts, ChecklistTemplatesQueries templates, PayeesQueries payees,
            ArchiveLoader loader, IArchiveFiles files) : base(db, logger)
        {
            _vouchers = vouchers;
            _advances = advances;
            _accounts = accounts;
            _templates = templates;
            _payees = payees;
            _loader = loader;
            _files = files;
        }

        public Task<List<DvDto>> GetAllAsync(bool includeDrafts = false) => _loader.GetVouchersAsync(includeDrafts: includeDrafts);

        public async Task<DvDto?> GetAsync(long id) => (await _loader.GetVouchersAsync(id)).FirstOrDefault();

        public async Task<ServiceResult> SaveAsync(SaveDvRequest r, long byUserId)
        {
            var dvNo = Regex.Replace((r.DvNo ?? "").Trim().ToUpperInvariant(), @"\s+", "");
            var payee = Regex.Replace((r.Payee ?? "").Trim(), @"\s+", " ");

            // ---- the voucher itself
            var ca = await _advances.SelectByIdAsync(r.CashAdvanceId);
            if (ca is null) return ServiceResult.Fail("Please choose the cash advance this DV is charged to.");
            if (dvNo.Length == 0 || dvNo.Length > 30)
                return ServiceResult.Fail("Please enter the DV number (up to 30 characters).");
            if (r.Date is null || r.Date.Value.Year is < 2000 or > 2100)
                return ServiceResult.Fail("Please enter the DV date.");
            if (payee.Length == 0 || payee.Length > 200)
                return ServiceResult.Fail("Please enter the payee (up to 200 characters).");
            if (await _vouchers.DvNoExistsAsync(dvNo, r.Id))
                return ServiceResult.Fail($"DV number {dvNo} is already in the archive.");

            // ---- account lines (rows left completely blank on the form are ignored)
            var typed = (r.Lines ?? []).Where(l => !string.IsNullOrWhiteSpace(l.Code) || (l.Amount ?? 0) != 0).ToList();
            if (typed.Count == 0)
                return ServiceResult.Fail("Please add at least one expense account with its amount.");
            if (typed.Count > MaxLines)
                return ServiceResult.Fail($"A DV can have up to {MaxLines} account lines.");
            if (typed.Any(l => string.IsNullOrWhiteSpace(l.Code)))
                return ServiceResult.Fail("Every amount needs a UACS account. Please choose the account on each line.");
            if (typed.Any(l => l.Amount is null or <= 0 or > MaxAmount))
                return ServiceResult.Fail("Every account line needs an amount more than 0.");

            var known = (await _accounts.GetAllSortedAsync()).ToDictionary(a => a.Code);
            var lines = new List<DvLines>();
            foreach (var l in typed)
            {
                if (!known.TryGetValue(l.Code!.Trim(), out var account))
                    return ServiceResult.Fail($"UACS account {l.Code} is not in Setup → UACS Accounts.");
                lines.Add(new DvLines { SortOrder = lines.Count + 1, Code = account.Code, Title = account.Title, Amount = l.Amount!.Value });
            }

            var gross = lines.Sum(l => l.Amount);
            decimal tax1 = r.Tax1 ?? 0, tax3 = r.Tax3 ?? 0;
            if (tax1 < 0 || tax3 < 0) return ServiceResult.Fail("Withholding tax cannot be negative.");
            if (tax1 + tax3 > gross) return ServiceResult.Fail("The withholding tax is more than the gross amount.");

            // ---- transaction type (checklist template)
            ChecklistTemplates? template = null;
            if (r.Type is not null)
            {
                template = await _templates.SelectByIdAsync(r.Type.Value);
                if (template is null) return ServiceResult.Fail("That transaction type no longer exists. Please choose another.");
            }

            var checklist = (r.Check ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Label)).Take(MaxChecklist)
                .Select((c, i) => new DvChecklistItems
                {
                    SortOrder = i + 1, Label = Cut(c.Label, 300)!, Mark = ArchiveLoader.ParseMark(c.Mark), DateText = Cut(c.Date, 20)
                }).ToList();

            // ---- save
            _vouchers.UserId = byUserId;
            DisbursementVouchers dv;
            if (r.Id == 0)
            {
                dv = new DisbursementVouchers();
                _vouchers.Add(dv);
            }
            else
            {
                var existing = await _vouchers.GetForEditWithDetailsAsync(r.Id);
                if (existing is null) return ServiceResult.Fail("That DV no longer exists.");
                dv = existing;
                dv.Lines.Clear();
                dv.Checklist.Clear();
            }

            dv.IsDraft = false;
            dv.CashAdvanceId = ca.Id;
            dv.DvNo = dvNo;
            dv.Date = r.Date.Value.Date;
            dv.Type = template?.Name;
            dv.ChecklistTemplateId = template?.Id;
            dv.Payee = payee;
            dv.Tin = Cut(r.Tin, 30);
            dv.Address = Cut(r.Address, 250);
            dv.Particulars = Cut(r.Particulars, 1000);
            dv.FundCluster = Cut(r.FundCluster, 50);
            dv.ResponsibilityCenter = Cut(r.ResponsibilityCenter, 50);
            dv.MfoPap = Cut(r.MfoPap, 50);
            dv.OrsBurs = Cut(r.OrsBurs, 50);
            dv.Tax1 = tax1;
            dv.Tax3 = tax3;
            dv.Mode = Cut(r.Mode, 30);
            dv.CheckNo = Cut(r.CheckNo, 30);
            dv.PaidDate = r.PaidDate?.Date;
            dv.Bank = Cut(r.Bank, 100);
            dv.ReceivedBy = Cut(r.ReceivedBy, 150);
            dv.OrNo = Cut(r.OrNo, 50);
            dv.JevNo = Cut(r.JevNo, 50);
            dv.ChecklistRemarks = Cut(r.ChecklistRemarks, 500);
            dv.Lines.AddRange(lines);
            dv.Checklist.AddRange(checklist);

            // A payee typed for the first time is added to Setup → Payees, so next time it can be picked.
            if (!await _payees.NameExistsAsync(payee))
                _payees.Add(new Payees { Name = payee, Tin = dv.Tin, Address = dv.Address });

            await _vouchers.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} saved DV {dvNo} ({payee}, gross {gross:N2}).");
            return ServiceResult.Ok(dv.Id);
        }

        public async Task<ServiceResult> SaveChecklistAsync(long id, List<ChecklistInput> marks, string? remarks, long byUserId)
        {
            _vouchers.UserId = byUserId;
            var dv = await _vouchers.GetForEditWithDetailsAsync(id);
            if (dv is null) return ServiceResult.Fail("That DV no longer exists.");

            // The form sends the items in the same order they were shown.
            var items = dv.Checklist.OrderBy(c => c.SortOrder).ToList();
            if (marks.Count != items.Count)
                return ServiceResult.Fail("The checklist was changed by someone else. Please open the DV again.");
            for (var i = 0; i < items.Count; i++)
            {
                items[i].Mark = ArchiveLoader.ParseMark(marks[i].Mark);
                items[i].DateText = Cut(marks[i].Date, 20);
            }
            dv.ChecklistRemarks = Cut(remarks, 500);
            // Counts as a change to the DV even when only checklist rows changed.
            dv.UpdatedAt = DateTime.Now;
            dv.UpdatedBy = byUserId;
            await _vouchers.SaveChangesAsync();

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} updated the checklist of DV {dv.DvNo ?? "#" + dv.Id}.");
            return ServiceResult.Ok(id);
        }

        public async Task<ServiceResult> CreateDraftAsync(long cashAdvanceId, long? templateId, long byUserId)
        {
            var ca = await _advances.SelectByIdAsync(cashAdvanceId);
            if (ca is null) return ServiceResult.Fail("Please choose the cash advance this DV is charged to.");

            ChecklistTemplates? template = null;
            if (templateId is not null)
            {
                template = await _templates.GetWithItemsAsync(templateId.Value);
                if (template is null) return ServiceResult.Fail("That transaction type no longer exists. Please choose another.");
            }

            var dv = new DisbursementVouchers
            {
                IsDraft = true, CashAdvanceId = ca.Id, Date = DateTime.Today,
                Type = template?.Name, ChecklistTemplateId = template?.Id
            };
            if (template is not null)
                dv.Checklist.AddRange(template.Items.OrderBy(i => i.SortOrder)
                    .Select((i, n) => new DvChecklistItems { SortOrder = n + 1, Label = i.Label }));

            _vouchers.UserId = byUserId;
            var id = await _vouchers.CreateAsync(dv);
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} started draft DV #{id} from an uploaded scan.");
            return ServiceResult.Ok(id);
        }

        public async Task<ServiceResult> DeleteAsync(long id, long byUserId)
        {
            var dv = await _vouchers.SelectByIdAsync(id);
            if (dv is null) return ServiceResult.Fail("That DV no longer exists.");

            await _files.DeleteForOwnerAsync(FileOwner.Voucher, id);
            await _vouchers.DeleteByIdAsync(id);   // its lines and checklist go with it
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} deleted DV {dv.DvNo ?? "draft #" + id}.");
            return ServiceResult.Ok();
        }

        public async Task<Dictionary<long, string>> GetNextDvNumbersAsync()
        {
            var used = await _vouchers.GetAllDvNosAsync();
            var result = new Dictionary<long, string>();
            foreach (var ca in await _advances.GetAllSortedAsync())
            {
                // The running number continues through the year for the department, whatever the batch:
                // JHS2026-03-053 is followed by JHS2026-04-054.
                var series = $"{ca.Department}{ca.Date.Year}-";
                var last = used.Where(n => n.StartsWith(series, StringComparison.OrdinalIgnoreCase))
                    .Select(n => int.TryParse(n[(n.LastIndexOf('-') + 1)..], out var number) ? number : 0)
                    .DefaultIfEmpty(0).Max();
                result[ca.Id] = $"{series}{ca.BatchNo:00}-{last + 1:000}";
            }
            return result;
        }

        public async Task<ArchiveAlerts> GetAlertsAsync() =>
            new(await _vouchers.CountDraftsAsync(), await _vouchers.CountIncompleteAsync());

        /// <summary>Trimmed and cut to the column's size; empty becomes null.</summary>
        private static string? Cut(string? text, int max)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;
            return text.Length > max ? text[..max] : text;
        }
    }
}
