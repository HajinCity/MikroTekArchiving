using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>Staff accounts: signing in and out, and managing who can use the portal.</summary>
    public interface IUsers
    {
        Task<SignInResult> SignInAsync(string? username, string? password);
        Task SignOutAsync(long userId);

        Task<UserDto?> GetAsync(long id);
        Task<List<UserDto>> GetAllAsync();

        /// <summary>Adds (Id = 0) or updates a staff account. byUserId = the admin doing it.</summary>
        Task<ServiceResult> SaveAsync(SaveUserRequest request, long byUserId);

        Task<ServiceResult> ResetPasswordAsync(long id, string? newPassword);
        Task<ServiceResult> ChangeOwnPasswordAsync(long id, string? currentPassword, string? newPassword);
    }
}
