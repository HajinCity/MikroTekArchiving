using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class HotspotSessionsQueries(DB db, Logger logger) : CoreQueries<HotspotSessions>(db, logger)
    {
        /// <summary>The device's current (not logged out) session with its voucher and plan (tracked).</summary>
        public Task<HotspotSessions?> GetOpenSessionAsync(string macAddress) =>
            Set.Include(s => s.Voucher).ThenInclude(v => v.Plan)
               .Where(s => s.MacAddress == macAddress && s.LogoutAt == null)
               .OrderByDescending(s => s.LoginAt)
               .FirstOrDefaultAsync();

        /// <summary>All open sessions of one device (tracked).</summary>
        public Task<List<HotspotSessions>> GetOpenByMacAsync(string macAddress) =>
            Set.Where(s => s.MacAddress == macAddress && s.LogoutAt == null).ToListAsync();

        /// <summary>Sessions still open although their voucher is no longer active (e.g. disabled by an admin).</summary>
        public Task<List<HotspotSessions>> GetOrphanedOpenAsync() =>
            Set.Where(s => s.LogoutAt == null && s.Voucher.Status != VoucherStatus.Active).ToListAsync();
    }
}
