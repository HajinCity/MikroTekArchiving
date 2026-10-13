using MikroTek.Services.DTO;

namespace MikroTek.Archiving.Areas.Portal.Models;

/// <summary>The Record / Edit Cash Advance page: the typed values, and the message if saving failed.</summary>
public class CashAdvanceForm
{
    public required SaveCashAdvanceRequest Values { get; init; }
    public string? Error { get; init; }
    public bool IsNew => Values.Id == 0;
}

/// <summary>
/// The DV encoding page (new, edit, and a draft started from an uploaded scan).
/// Values = what is in the boxes (kept when saving fails, so nothing has to be typed again).
/// </summary>
public class DvForm
{
    public required SaveDvRequest Values { get; init; }
    public string? Error { get; init; }
    /// <summary>The DV being edited; null when encoding a new one.</summary>
    public DvDto? Existing { get; init; }

    public required List<CashAdvanceDto> CashAdvances { get; init; }
    public required List<ChecklistTemplateDto> Templates { get; init; }
    public required List<UacsAccountDto> Accounts { get; init; }
    public required List<PayeeDto> Payees { get; init; }
    public required Dictionary<long, string> NextNumbers { get; init; }
    public required SchoolProfileDto School { get; init; }
    /// <summary>The liability account the withheld taxes are credited to (Due to BIR).</summary>
    public required string BirAccountTitle { get; init; }
    /// <summary>Scans already attached (shown beside the form so the values can be copied from them).</summary>
    public List<ArchiveFileDto> Scans { get; init; } = [];

    public bool IsNew => Existing is null;
    public bool IsDraft => Existing?.IsDraft == true;
}

/// <summary>The Upload Scanned DV page.</summary>
public class UploadPage
{
    public required List<CashAdvanceDto> CashAdvances { get; init; }
    public required List<ChecklistTemplateDto> Templates { get; init; }
    public required List<DvDto> Drafts { get; init; }
    public long? SelectedCashAdvanceId { get; init; }
}

/// <summary>The Liquidation Reports page.</summary>
public class ReportsPage
{
    public required List<CashAdvanceDto> CashAdvances { get; init; }
    public required List<DvDto> Vouchers { get; init; }
}

/// <summary>Import MOOE Packet: the upload page.</summary>
public record ImportPage(ScanReaderStatus Status);

/// <summary>Import MOOE Packet: what was read, to check beside the scan before saving.</summary>
public class ImportReview
{
    public required PacketDraft Draft { get; init; }
    public required List<UacsAccountDto> Accounts { get; init; }
    public string? Error { get; init; }
}
