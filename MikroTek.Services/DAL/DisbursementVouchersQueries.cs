using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class DisbursementVouchersQueries(DB db, Logger logger) : CoreQueries<DisbursementVouchers>(db, logger)
    {
        /// <summary>
        /// DVs with their account lines, checklist and cash advance (read-only).
        /// id = just that one; cashAdvanceId = only that advance's.
        /// </summary>
        public Task<List<DisbursementVouchers>> GetDetailedAsync(long? id = null, long? cashAdvanceId = null)
        {
            var query = Query.Include(v => v.Lines).Include(v => v.Checklist).Include(v => v.CashAdvance).AsSplitQuery();
            if (id is not null) query = query.Where(v => v.Id == id);
            if (cashAdvanceId is not null) query = query.Where(v => v.CashAdvanceId == cashAdvanceId);
            return query.OrderBy(v => v.Date).ThenBy(v => v.DvNo).ToListAsync();
        }

        /// <summary>Tracked, with lines and checklist, so the whole voucher can be changed and saved together.</summary>
        public Task<DisbursementVouchers?> GetForEditWithDetailsAsync(long id) =>
            Set.Include(v => v.Lines).Include(v => v.Checklist).FirstOrDefaultAsync(v => v.Id == id);

        public Task<bool> DvNoExistsAsync(string dvNo, long excludeId = 0) =>
            AnyAsync(v => v.DvNo == dvNo && v.Id != excludeId);

        /// <summary>Every DV number already used (to suggest the next one).</summary>
        public Task<List<string>> GetAllDvNosAsync() =>
            Query.Where(v => v.DvNo != null).Select(v => v.DvNo!).ToListAsync();

        public Task<int> CountDraftsAsync() => CountAsync(v => v.IsDraft);

        /// <summary>Encoded DVs that still miss a document or have no scan attached.</summary>
        public Task<int> CountIncompleteAsync() =>
            CountAsync(v => !v.IsDraft
                && (v.Checklist.Any(c => c.Mark == ChecklistMark.Missing)
                    || !Db.Set<ArchiveFiles>().Any(f => f.OwnerType == FileOwner.Voucher && f.OwnerId == v.Id)));
    }
}
