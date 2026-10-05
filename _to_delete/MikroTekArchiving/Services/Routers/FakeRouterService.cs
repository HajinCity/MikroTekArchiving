using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace MikroTekArchiving.Services.Routers;


public class FakeRouterService(ILogger<FakeRouterService> logger) : IRouterService
{
    private const string DeviceCookie = "fake_device_mac";
    private readonly ConcurrentDictionary<string, RouterActiveUser> _allowed = new();

    public HotspotClient GetClient(HttpContext http)
    {
        var mac = http.Request.Cookies[DeviceCookie];
        if (string.IsNullOrEmpty(mac))
        {
            mac = NewFakeMac();
            http.Response.Cookies.Append(DeviceCookie, mac, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Expires = DateTimeOffset.Now.AddYears(1)
            });
        }

        return new HotspotClient(mac, FormatIp(http.Connection.RemoteIpAddress));
    }

    public Task GrantAccessAsync(HotspotClient client, string voucherCode, DateTime expiresAt)
    {
        _allowed[client.MacAddress] =
            new RouterActiveUser(client.MacAddress, client.IpAddress, voucherCode, DateTime.Now, expiresAt);
        logger.LogInformation("[FakeRouter] ALLOW {Mac} ({Ip}) with {Code} until {Expires}",
            client.MacAddress, client.IpAddress, voucherCode, expiresAt);
        return Task.CompletedTask;
    }

    public Task RevokeAccessAsync(string macAddress)
    {
        if (_allowed.TryRemove(macAddress, out _))
            logger.LogInformation("[FakeRouter] BLOCK {Mac}", macAddress);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RouterActiveUser>> GetActiveUsersAsync() =>
        Task.FromResult<IReadOnlyList<RouterActiveUser>>(_allowed.Values.OrderBy(u => u.LoginAt).ToList());

    private static string NewFakeMac()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        bytes[0] = 0x02;
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static string FormatIp(IPAddress? ip)
    {
        if (ip is null) return "0.0.0.0";
        if (IPAddress.IsLoopback(ip)) return "127.0.0.1";
        return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
    }
}
