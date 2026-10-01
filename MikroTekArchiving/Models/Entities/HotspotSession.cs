using System.ComponentModel.DataAnnotations;

namespace MikroTekArchiving.Models.Entities;

/// <summary>One login of one device. LogoutAt is null while the device is still online.</summary>
public class HotspotSession
{
    public int Id { get; set; }

    public int VoucherId { get; set; }
    public Voucher Voucher { get; set; } = null!;

    [Required, StringLength(17)]
    public string MacAddress { get; set; } = "";

    [Required, StringLength(45)]
    public string IpAddress { get; set; } = "";

    public DateTime LoginAt { get; set; } = DateTime.Now;
    public DateTime? LogoutAt { get; set; }

    /// <summary>Why the session ended: "Logout", "Expired", "Disconnected by admin", ...</summary>
    [StringLength(50)]
    public string? EndReason { get; set; }
}
