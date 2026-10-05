using MikroTek.Common;
using MikroTek.Common.Utility;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class RoleServices : CoreServices, IRoles
    {
        // Access is checked on every page, so each role's list is remembered for a few minutes
        // instead of being read from the database every time. Saving clears it at once.
        private const int CacheMinutes = 10;
        private static string CacheKey(UserRole role) => "role-permissions:" + role;

        private readonly RolePermissionsQueries _permissions;

        public RoleServices(DB db, Logger logger, RolePermissionsQueries permissions) : base(db, logger)
        {
            _permissions = permissions;
        }

        public async Task<HashSet<string>> GetPermissionsAsync(UserRole role)
        {
            if (role == UserRole.Admin)
                return Permissions.All.Select(p => p.Key).ToHashSet();

            if (CachingUtility.Get<HashSet<string>>(CacheKey(role)) is { } cached)
                return cached;

            var saved = await _permissions.FindAsync(p => p.Role == role);
            var allowed = Clean(saved.Select(p => p.Permission));
            CachingUtility.SetUp(CacheKey(role), allowed, CacheMinutes);
            return allowed;
        }

        public async Task<ServiceResult> SaveAsync(Dictionary<UserRole, List<string>> ticks, long byUserId)
        {
            _permissions.UserId = byUserId;
            foreach (var (role, wanted) in ticks)
            {
                if (role == UserRole.Admin) continue;

                var keep = Clean(wanted);
                var saved = await _permissions.GetByRoleAsync(role);
                foreach (var row in saved.Where(s => !keep.Contains(s.Permission)))
                    _permissions.Remove(row);
                foreach (var key in keep.Where(k => saved.All(s => s.Permission != k)))
                    _permissions.Add(new RolePermissions { Role = role, Permission = key });
            }
            await _permissions.SaveChangesAsync();

            foreach (var role in ticks.Keys)
                CachingUtility.Remove(CacheKey(role));

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} changed the access of: {string.Join(", ", ticks.Keys)}.");
            return ServiceResult.Ok();
        }

        /// <summary>Drops unknown and admin-only keys, and adds what a ticked feature depends on.</summary>
        private static HashSet<string> Clean(IEnumerable<string> keys)
        {
            var known = Permissions.All.Select(p => p.Key).ToHashSet();
            var result = keys.Where(k => known.Contains(k) && !Permissions.AdminOnly.Contains(k)).ToHashSet();
            foreach (var (feature, needs) in Permissions.Requires)
                if (result.Contains(feature)) result.Add(needs);
            return result;
        }
    }
}
