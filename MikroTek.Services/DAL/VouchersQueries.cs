using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class VouchersQueries(DB db, Logger logger) : CoreQueries<Vouchers>(db, logger)
    {
        /// <summary>The voucher with its plan (tracked, so login can change and save it).</summary>
        public Task<Vouchers?> GetByCodeWithPlanAsync(string code) =>
            Set.Include(v => v.Plan).FirstOrDefaultAsync(v => v.VoucherCode == code);

        /// <summary>Active vouchers whose time is up, with their open sessions (tracked).</summary>
        public Task<List<Vouchers>> GetDueForExpiryAsync(DateTime now) =>
            Set.Include(v => v.Sessions.Where(s => s.LogoutAt == null))
               .Where(v => v.Status == VoucherStatus.Active && v.ExpiresAt <= now)
               .ToListAsync();

        public async Task<HashSet<string>> GetAllCodesAsync() =>
            new(await Query.Select(v => v.VoucherCode).ToListAsync());
    }
}
