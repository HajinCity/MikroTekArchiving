using System.Net;
using System.Security.Cryptography;
using MikroTek.Services.DTO;

namespace MikroTekArchiving.Utility;

/// <summary>
/// Works out which device is asking (its MAC and IP) from the web request.
/// Without a MikroTik we cannot see real MAC addresses, so each browser gets a made-up one
/// that is kept in a cookie. With a real MikroTik, the router passes the MAC in the login link instead.
/// </summary>
public static class HotspotDevice
{
    private const string DeviceCookie = "fake_device_mac";

    public static HotspotClient GetClient(HttpContext http)
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

    private static string NewFakeMac()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        bytes[0] = 0x02; // "locally administered" so it never matches a real device
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static string FormatIp(IPAddress? ip)
    {
        if (ip is null) return "0.0.0.0";
        if (IPAddress.IsLoopback(ip)) return "127.0.0.1";
        return ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4().ToString() : ip.ToString();
    }
}
