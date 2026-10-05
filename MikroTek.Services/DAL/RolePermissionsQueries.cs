using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class RolePermissionsQueries(DB db, Logger logger) : CoreQueries<RolePermissions>(db, logger)
    {
        /// <summary>The ticked features of one role (tracked, so ticks can be added and removed).</summary>
        public Task<List<RolePermissions>> GetByRoleAsync(UserRole role) =>
            Set.Where(p => p.Role == role).ToListAsync();
    }
}
