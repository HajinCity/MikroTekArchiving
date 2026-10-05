using MikroTek.Domain;
using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>The scanned documents attached to disbursement vouchers and cash advances.</summary>
    public interface IArchiveFiles
    {
        Task<List<ArchiveFileDto>> GetForOwnerAsync(FileOwner ownerType, long ownerId);

        /// <summary>Checks the file (PDF, JPG or PNG; size) and stores it.</summary>
        Task<ServiceResult> AddAsync(FileOwner ownerType, long ownerId, string fileName, long sizeBytes, Stream content, long byUserId);

        /// <summary>The file's details and where it is on the server, or null if it is gone.</summary>
        Task<StoredFile?> GetAsync(long id);

        /// <summary>Just the details (to check who may touch it), or null.</summary>
        Task<ArchiveFileDto?> GetInfoAsync(long id);

        /// <summary>Removes the file from its record and from the server.</summary>
        Task<ServiceResult> DeleteAsync(long id, long byUserId);
    }
}
