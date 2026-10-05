using System.ComponentModel.DataAnnotations;

namespace MikroTekArchiving.Models.Entities;

/// <summary>A voucher type, e.g. "1 Hour" or "1 Day". Every voucher belongs to one plan.</summary>
public class Plan
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    /// <summary>How long a voucher lasts after its first login (continuous time).</summary>
    [Range(1, 525600)]
    public int DurationMinutes { get; set; }

    [Range(0, 100000)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
