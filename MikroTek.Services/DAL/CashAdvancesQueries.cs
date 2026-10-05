using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class CashAdvancesQueries(DB db, Logger logger) : CoreQueries<CashAdvances>(db, logger)
    {
        /// <summary>Newest first.</summary>
        public Task<List<CashAdvances>> GetAllSortedAsync() =>
            Query.OrderByDescending(c => c.Date).ThenByDescending(c => c.Id).ToListAsync();

        public Task<bool> PayrollNoExistsAsync(string payrollNo, long excludeId = 0) =>
            AnyAsync(c => c.PayrollNo == payrollNo && c.Id != excludeId);

        /// <summary>How many DVs (drafts too) are charged to this cash advance.</summary>
        public Task<int> CountVouchersAsync(long id) =>
            Db.Set<DisbursementVouchers>().CountAsync(v => v.CashAdvanceId == id);
    }
}
