using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>The chart of accounts (Setup → UACS Accounts).</summary>
    public interface IUacsAccounts
    {
        Task<List<UacsAccountDto>> GetAllAsync();

        /// <summary>Adds (Id = 0) or updates an account. byUserId = the person doing it.</summary>
        Task<ServiceResult> SaveAsync(SaveUacsAccountRequest request, long byUserId);
    }
}
