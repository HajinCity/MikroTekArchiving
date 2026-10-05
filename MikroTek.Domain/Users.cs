using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>Everyone who can sign in: hotspot admin and portal staff (Admin / Bookkeeper / Viewer).</summary>
    public class Users : BaseEntity
    {
        public string Username { get; set; } = "";

        /// <summary>Scrambled (hashed) password from Encryption.HashPassword, never the typed password.</summary>
        public string Password { get; set; } = "";

        public string Name { get; set; } = "";

        /// <summary>Job title shown on reports, e.g. "ADAS III" or "Bookkeeper".</summary>
        public string? Position { get; set; }
        public UserRole Role { get; set; } = UserRole.Viewer;
        public DateTime? LastLoginTime { get; set; }
        public DateTime? LastLogoutTime { get; set; }

        /// <summary>true = can sign in, false = blocked.</summary>
        public bool ActiveStatus { get; set; } = true;
    }
}
