using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class UacsAccountsQueries(DB db, Logger logger) : CoreQueries<UacsAccounts>(db, logger)
    {
        public Task<List<UacsAccounts>> GetAllSortedAsync() =>
            Query.OrderBy(a => a.Code).ToListAsync();

        /// <summary>True if another account (not excludeId) already uses this code.</summary>
        public Task<bool> CodeExistsAsync(string code, long excludeId = 0) =>
            AnyAsync(a => a.Code == code && a.Id != excludeId);
    }
}
