using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>
    /// Talks to the router that actually lets devices onto the internet.
    /// Today: FakeRouterService (no MikroTik yet). Later: a MikroTik version with the same methods.
    /// </summary>
    public interface IRouterService
    {
        Task GrantAccessAsync(HotspotClient client, string voucherCode, DateTime expiresAt);
        Task RevokeAccessAsync(string macAddress);
        Task<IReadOnlyList<RouterActiveUser>> GetActiveUsersAsync();
    }
}
