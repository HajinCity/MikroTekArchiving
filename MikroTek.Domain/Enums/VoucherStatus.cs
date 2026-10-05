namespace MikroTek.Domain
{
    public enum VoucherStatus
    {
        /// <summary>Generated but never used.</summary>
        Unused,
        /// <summary>Used at least once and the time has not run out yet.</summary>
        Active,
        /// <summary>Time has run out.</summary>
        Expired,
        /// <summary>Blocked by an admin.</summary>
        Disabled
    }
}
