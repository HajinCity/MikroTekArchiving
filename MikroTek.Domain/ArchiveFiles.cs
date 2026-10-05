using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>
    /// A scanned document (PDF or photo) attached to a disbursement voucher or a cash advance.
    /// Only the details are in the database; the file itself is kept in the uploads folder.
    /// </summary>
    public class ArchiveFiles : AuditableEntity
    {
        /// <summary>Voucher or CashAdvance.</summary>
        public FileOwner OwnerType { get; set; }

        /// <summary>The Id of that voucher or cash advance.</summary>
        public long OwnerId { get; set; }

        /// <summary>The name the file had on the uploader's computer (shown on screen).</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>The name it is saved under on the server (random, so nothing can be overwritten or guessed).</summary>
        public string StoredName { get; set; } = string.Empty;

        /// <summary>"application/pdf", "image/jpeg" or "image/png".</summary>
        public string ContentType { get; set; } = string.Empty;

        public long SizeBytes { get; set; }
    }
}
