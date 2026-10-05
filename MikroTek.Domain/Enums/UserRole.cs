namespace MikroTek.Domain
{
    public enum UserRole
    {
        /// <summary>Can only look at records and print.</summary>
        Viewer,
        /// <summary>Encodes and archives disbursement vouchers.</summary>
        Bookkeeper,
        /// <summary>Everything, including setup, users and the hotspot.</summary>
        Admin
    }
}
