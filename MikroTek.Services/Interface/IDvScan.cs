using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>Reads the scanned files attached to a DV and suggests what to put in its form.</summary>
    public interface IDvScan
    {
        /// <summary>
        /// Reads the DV's attached scans (or returns the earlier reading of the same files, unless again = true).
        /// Never throws for a bad scan: Found = false and Message says why.
        /// </summary>
        Task<DvScanResult> ReadAsync(long voucherId, bool again, long byUserId);
    }
}
