using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>One row per login of a device with a voucher.</summary>
    public class HotspotSessions : BaseEntity
    {
        public long VoucherId { get; set; }
        public Vouchers Voucher { get; set; } = null!;

        public string MacAddress { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public DateTime LoginAt { get; set; } = DateTime.Now;

        /// <summary>Empty while the device is still online.</summary>
        public DateTime? LogoutAt { get; set; }

        public string? EndReason { get; set; }
    }
}
