namespace MikroTek.Services.DTO
{
    /// <summary>The device asking for internet: its MAC address and IP address.</summary>
    public record HotspotClient(string MacAddress, string IpAddress);

    /// <summary>A device the router is currently letting through.</summary>
    public record RouterActiveUser(string MacAddress, string IpAddress, string VoucherCode,
                                   DateTime LoginAt, DateTime ExpiresAt);

    public record LoginResult(bool Success, string? Error = null);

    /// <summary>What the hotspot Status page shows for a logged-in device.</summary>
    public record SessionStatus(
        string VoucherCode, string PlanName, string MacAddress, string IpAddress,
        DateTime LoginAt, DateTime ExpiresAt)
    {
        public TimeSpan Uptime => DateTime.Now - LoginAt;
        public TimeSpan TimeLeft => ExpiresAt > DateTime.Now ? ExpiresAt - DateTime.Now : TimeSpan.Zero;
    }

    /// <summary>What the hotspot Logout page shows after a device logs off.</summary>
    public record LogoutSummary(
        string VoucherCode, string MacAddress, string IpAddress, TimeSpan SessionTime, TimeSpan TimeLeft);
}
