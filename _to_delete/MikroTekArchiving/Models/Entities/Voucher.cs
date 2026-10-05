using System.ComponentModel.DataAnnotations;

namespace MikroTekArchiving.Models.Entities;

public class Voucher
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string Code { get; set; } = "";

    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public VoucherStatus Status { get; set; } = VoucherStatus.Unused;

    /// <summary>Groups vouchers generated together, e.g. "B20260929-01".</summary>
    [StringLength(30)]
    public string? BatchNo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [StringLength(50)]
    public string? CreatedBy { get; set; }

    /// <summary>When the voucher was first used. The clock starts here.</summary>
    public DateTime? FirstUsedAt { get; set; }

    /// <summary>FirstUsedAt + plan duration. After this the voucher is expired.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>The device the voucher is locked to (set on first login).</summary>
    [StringLength(17)]
    public string? MacAddress { get; set; }

    public ICollection<HotspotSession> Sessions { get; set; } = new List<HotspotSession>();
}
