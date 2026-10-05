using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    /// <summary>An uploaded document as the pages see it.</summary>
    public record ArchiveFileDto(long Id, FileOwner OwnerType, long OwnerId, string FileName, string ContentType,
                                 long SizeBytes, DateTime UploadedAt, string? UploadedBy)
    {
        public bool IsPdf => ContentType == "application/pdf";

        /// <summary>"1.4 MB", "320 KB", ...</summary>
        public string SizeText => SizeBytes >= 1024 * 1024 ? $"{SizeBytes / 1048576.0:0.0} MB" : $"{Math.Max(1, SizeBytes / 1024)} KB";
    }

    /// <summary>A stored file ready to be sent to the browser.</summary>
    public record StoredFile(ArchiveFileDto Info, string FullPath);

    /// <summary>Where uploaded documents are kept and how big one may be. Set in Program.cs from appsettings "Uploads".</summary>
    public class UploadOptions
    {
        public string Folder { get; set; } = "";
        public long MaxFileBytes { get; set; } = 100L * 1024 * 1024;
    }
}
