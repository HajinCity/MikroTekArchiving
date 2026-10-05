using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    public class Vouchers : BaseEntity
    {
        public string VoucherCode { get; set; } = "";

        public long PlanId { get; set; }
        public Plans Plan { get; set; } = null!;

        public VoucherStatus Status { get; set; } = VoucherStatus.Unused;
        public string? BatchNo { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Username of who generated it ("seed" for the test vouchers).</summary>
        public string? CreatedBy { get; set; }

        /// <summary>Empty until the voucher is used for the first time.</summary>
        public DateTime? FirstUsedAt { get; set; }

        /// <summary>FirstUsedAt + plan duration. Empty until first use.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>The device the voucher is locked to after first use.</summary>
        public string? MacAddress { get; set; }

        public ICollection<HotspotSessions> Sessions { get; set; } = new List<HotspotSessions>();
    }
}
