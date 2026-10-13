using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>
    /// Creates a cash advance and its DVs from one scanned MOOE packet (MOOE slip, Alphalist and Cash Disbursements Register).
    /// The packet is read, kept with what was read until someone has checked it, then saved in one go.
    /// </summary>
    public interface IPacketImport
    {
        /// <summary>Whether the scan reader is installed on the server.</summary>
        Task<ScanReaderStatus> GetStatusAsync();

        /// <summary>Reads the packet and keeps it for review. Result.Error explains a file that cannot be read.</summary>
        Task<(ServiceResult Result, PacketDraft? Draft)> ReadAsync(string fileName, long sizeBytes, Stream content, long byUserId);

        /// <summary>What was read from a packet waiting for review, or null when it is gone (saved, discarded or expired).</summary>
        Task<PacketDraft?> GetDraftAsync(string token);

        /// <summary>Where the waiting packet's file is (to show it beside the review form), or null.</summary>
        string? GetPacketPath(string token);

        /// <summary>
        /// Checks the reviewed values and saves the cash advance (or uses the one already recorded) with every ticked DV,
        /// all or nothing; the packet file is attached to the cash advance. Result.Id = the cash advance's Id.
        /// </summary>
        Task<ServiceResult> SaveAsync(PacketDraft draft, long byUserId);

        /// <summary>Removes a waiting packet without saving anything.</summary>
        void Discard(string token);
    }
}
