using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class PayeesQueries(DB db, Logger logger) : CoreQueries<Payees>(db, logger)
    {
        public Task<List<Payees>> GetAllSortedAsync() => Query.OrderBy(p => p.Name).ToListAsync();

        public Task<bool> NameExistsAsync(string name, long excludeId = 0) =>
            AnyAsync(p => p.Name == name && p.Id != excludeId);

        /// <summary>How many encoded DVs each payee name appears on.</summary>
        public Task<Dictionary<string, int>> CountDvsByPayeeAsync() =>
            Db.Set<DisbursementVouchers>().AsNoTracking().Where(v => !v.IsDraft && v.Payee != null)
              .GroupBy(v => v.Payee!).Select(g => new { g.Key, Count = g.Count() })
              .ToDictionaryAsync(x => x.Key, x => x.Count, StringComparer.OrdinalIgnoreCase);
    }
}
