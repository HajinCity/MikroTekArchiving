using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>One ticked box on the Roles &amp; Access page: this role may use this feature.</summary>
    public class RolePermissions : AuditableEntity
    {
        public UserRole Role { get; set; }

        /// <summary>The feature's key, e.g. "vouchers.encode" (see MikroTek.Services.Utils.Permissions).</summary>
        public string Permission { get; set; } = string.Empty;
    }
}
