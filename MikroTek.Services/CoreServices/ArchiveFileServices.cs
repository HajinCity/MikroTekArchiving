using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class ArchiveFileServices : CoreServices, IArchiveFiles
    {
        private readonly ArchiveFilesQueries _files;
        private readonly UploadOptions _options;

        public ArchiveFileServices(DB db, Logger logger, ArchiveFilesQueries files, UploadOptions options) : base(db, logger)
        {
            _files = files;
            _options = options;
        }

        public Task<List<ArchiveFileDto>> GetForOwnerAsync(FileOwner ownerType, long ownerId) =>
            _files.GetForOwnerAsync(ownerType, ownerId);

        public async Task<ServiceResult> AddAsync(FileOwner ownerType, long ownerId, string fileName, long sizeBytes, Stream content, long byUserId)
        {
            fileName = Path.GetFileName(fileName ?? "").Trim();
            if (fileName.Length == 0) fileName = "scan";
            if (fileName.Length > 255) fileName = fileName[^255..];

            if (sizeBytes <= 0)
                return ServiceResult.Fail($"\"{fileName}\" is empty.");
            if (sizeBytes > _options.MaxFileBytes)
                return ServiceResult.Fail($"\"{fileName}\" is too big. The limit is {_options.MaxFileBytes / 1048576} MB per file.");

            // Decide the type from the first bytes of the file itself, not from its name,
            // so a renamed program or web page cannot be stored as a "PDF".
            var head = new byte[8];
            var read = await content.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false);
            var type = DetectType(head.AsSpan(0, read));
            if (type is null)
                return ServiceResult.Fail($"\"{fileName}\" is not a PDF, JPG or PNG file.");

            var folder = FolderFor(ownerType, ownerId);
            Directory.CreateDirectory(folder);
            var storedName = Guid.NewGuid().ToString("N") + type.Value.Extension;
            var path = Path.Combine(folder, storedName);
            try
            {
                await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await target.WriteAsync(head.AsMemory(0, read));
                    await content.CopyToAsync(target);
                }

                _files.UserId = byUserId;
                await _files.CreateAsync(new ArchiveFiles
                {
                    OwnerType = ownerType, OwnerId = ownerId, FileName = fileName, StoredName = storedName,
                    ContentType = type.Value.ContentType, SizeBytes = new FileInfo(path).Length
                });
            }
            catch
            {
                // Never leave a file on disk that the database does not know about.
                if (File.Exists(path)) File.Delete(path);
                throw;
            }

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} uploaded \"{fileName}\" to {ownerType} {ownerId}.");
            return ServiceResult.Ok();
        }

        public async Task<ArchiveFileDto?> GetInfoAsync(long id) =>
            await _files.SelectByIdAsync(id) is { } f ? ToDto(f) : null;

        public async Task<StoredFile?> GetAsync(long id)
        {
            var f = await _files.SelectByIdAsync(id);
            if (f is null) return null;

            var path = PathOf(f);
            if (!File.Exists(path))
            {
                _logger.WriteLog(Level.Warning, LogType.General, $"File {id} (\"{f.FileName}\") is missing from the uploads folder: {path}");
                return null;
            }
            return new StoredFile(ToDto(f), path);
        }

        public async Task<ServiceResult> DeleteAsync(long id, long byUserId)
        {
            var f = await _files.SelectByIdAsync(id);
            if (f is null) return ServiceResult.Fail("That file no longer exists.");

            await _files.DeleteByIdAsync(id);
            var path = PathOf(f);
            if (File.Exists(path)) File.Delete(path);

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} removed \"{f.FileName}\" from {f.OwnerType} {f.OwnerId}.");
            return ServiceResult.Ok();
        }

        public async Task DeleteForOwnerAsync(FileOwner ownerType, long ownerId)
        {
            foreach (var f in await _files.GetByOwnerAsync(ownerType, ownerId))
                await RemoveAsync(f);
        }

        public async Task<int> RemoveOrphansAsync()
        {
            var orphans = await _files.GetOrphansAsync();
            foreach (var f in orphans)
                await RemoveAsync(f);
            if (orphans.Count > 0)
                _logger.WriteLog(Level.Info, LogType.General, $"Removed {orphans.Count} uploaded file(s) whose record no longer exists.");
            return orphans.Count;
        }

        private async Task RemoveAsync(ArchiveFiles f)
        {
            await _files.DeleteByIdAsync(f.Id);
            var path = PathOf(f);
            if (File.Exists(path)) File.Delete(path);
        }

        // uploads/voucher/12/3f2a....pdf
        private string FolderFor(FileOwner ownerType, long ownerId) =>
            Path.Combine(_options.Folder, ownerType.ToString().ToLowerInvariant(), ownerId.ToString());

        private string PathOf(ArchiveFiles f) => Path.Combine(FolderFor(f.OwnerType, f.OwnerId), f.StoredName);

        private static ArchiveFileDto ToDto(ArchiveFiles f) =>
            new(f.Id, f.OwnerType, f.OwnerId, f.FileName, f.ContentType, f.SizeBytes, f.CreatedAt, null);

        private static (string ContentType, string Extension)? DetectType(ReadOnlySpan<byte> head)
        {
            if (head.StartsWith("%PDF-"u8)) return ("application/pdf", ".pdf");
            if (head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return ("image/jpeg", ".jpg");
            if (head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ("image/png", ".png");
            return null;
        }
    }
}
