using MikroTek.Domain;
using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>What each role is allowed to do (the Roles &amp; Access page).</summary>
    public interface IRoles
    {
        /// <summary>The features this role may use. System Admin always gets everything.</summary>
        Task<HashSet<string>> GetPermissionsAsync(UserRole role);

        /// <summary>Saves the ticks of every role sent (System Admin is never changed).</summary>
        Task<ServiceResult> SaveAsync(Dictionary<UserRole, List<string>> ticks, long byUserId);
    }
}
