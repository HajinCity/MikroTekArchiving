using System.ComponentModel.DataAnnotations;

namespace MikroTekArchiving.Models.Entities;

/// <summary>An administrator who can log in to the admin side.</summary>
public class AdminUser
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Username { get; set; } = "";

    [Required, StringLength(100)]
    public string FullName { get; set; } = "";

    /// <summary>Hashed with ASP.NET Core's PasswordHasher. Never store plain passwords.</summary>
    [Required]
    public string PasswordHash { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }
}
