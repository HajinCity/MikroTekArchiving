using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class UsersQueries(DB db, Logger logger) : CoreQueries<Users>(db, logger)
    {
        /// <summary>For sign-in (tracked, so LastLoginTime can be updated and saved).</summary>
        public Task<Users?> GetByUsernameAsync(string username) =>
            Set.FirstOrDefaultAsync(u => u.Username == username);

        /// <summary>True if another account (not excludeId) already uses this username.</summary>
        public Task<bool> UsernameExistsAsync(string username, long excludeId = 0) =>
            AnyAsync(u => u.Username == username && u.Id != excludeId);

        public Task<int> CountActiveAdminsAsync(long excludeId = 0) =>
            CountAsync(u => u.Role == UserRole.Admin && u.ActiveStatus && u.Id != excludeId);

        /// <summary>User Id → full name, to show who encoded or uploaded something.</summary>
        public Task<Dictionary<long, string>> GetNamesAsync() =>
            Query.ToDictionaryAsync(u => u.Id, u => u.Name);

        public Task<List<Users>> GetAllSortedAsync() =>
            Query.OrderByDescending(u => u.ActiveStatus).ThenBy(u => u.Name).ToListAsync();
    }
}
