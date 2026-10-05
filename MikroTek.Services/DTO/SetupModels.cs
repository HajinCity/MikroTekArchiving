using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    public record UacsAccountDto(long Id, string Code, string Title, AccountGroup Group, bool IsActive);

    /// <summary>Add (Id = 0) or edit a UACS account.</summary>
    public record SaveUacsAccountRequest(long Id, string? Code, string? Title, AccountGroup Group, bool IsActive);
}
