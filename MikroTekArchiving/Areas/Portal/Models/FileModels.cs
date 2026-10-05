using MikroTek.Domain;
using MikroTek.Services.DTO;

namespace MikroTek.Archiving.Areas.Portal.Models;

/// <summary>
/// What the shared "_Files" partial needs: the documents of one voucher or cash advance.
/// CanEdit = may upload and remove. Compact = small card style (no big drop area).
/// </summary>
public record FilesPanel(FileOwner Owner, long OwnerId, List<ArchiveFileDto> Files, bool CanEdit, bool Compact = false);
