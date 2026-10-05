using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>A voucher plan, e.g. "1 Hour" for 5 pesos.</summary>
    public class Plans : BaseEntity
    {
        public string Name { get; set; } = "";

        /// <summary>How long a voucher lasts, in minutes.</summary>
        public int Duration { get; set; }

        public decimal PlanPrice { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Vouchers> Vouchers { get; set; } = new List<Vouchers>();
    }
}
