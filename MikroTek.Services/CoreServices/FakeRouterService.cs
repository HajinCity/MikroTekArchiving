using System.Collections.Concurrent;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    /// <summary>
    /// Pretend router used until a real MikroTik is connected. It only remembers which devices
    /// are "allowed" and writes to the log what a real router would do.
    /// </summary>
    public class FakeRouterService(Logger logger) : IRouterService
    {
        private readonly ConcurrentDictionary<string, RouterActiveUser> _allowed = new();

        public Task GrantAccessAsync(HotspotClient client, string voucherCode, DateTime expiresAt)
        {
            _allowed[client.MacAddress] =
                new RouterActiveUser(client.MacAddress, client.IpAddress, voucherCode, DateTime.Now, expiresAt);
            logger.WriteLog(Level.Info, LogType.Activity,
                $"[FakeRouter] ALLOW {client.MacAddress} ({client.IpAddress}) with {voucherCode} until {expiresAt:g}");
            return Task.CompletedTask;
        }

        public Task RevokeAccessAsync(string macAddress)
        {
            if (_allowed.TryRemove(macAddress, out _))
                logger.WriteLog(Level.Info, LogType.Activity, $"[FakeRouter] BLOCK {macAddress}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RouterActiveUser>> GetActiveUsersAsync() =>
            Task.FromResult<IReadOnlyList<RouterActiveUser>>(_allowed.Values.OrderBy(u => u.LoginAt).ToList());
    }
}
