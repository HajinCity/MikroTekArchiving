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

        /// <summary>
        /// An admin gives the account a temporary password. The person must choose their own
        /// password the next time they sign in (not when the admin resets their own).
        /// </summary>
        Task<ServiceResult> ResetPasswordAsync(long id, string? newPassword, long byUserId);

        /// <summary>A person edits their own name and position (not the username or role).</summary>
        Task<ServiceResult> UpdateOwnProfileAsync(long id, string? name, string? position);
        Task<ServiceResult> ChangeOwnPasswordAsync(long id, string? currentPassword, string? newPassword);
    }
}
