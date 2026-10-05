using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    /// <summary>A staff account as the pages see it (never includes the password).</summary>
    public record UserDto(long Id, string Username, string Name, string? Position, UserRole Role,
                          bool ActiveStatus, DateTime? LastLoginTime, DateTime? LastLogoutTime,
                          bool MustChangePassword = false)
    {
        public string Initials =>
            string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpper(p[0])));
    }

    public record SignInResult(bool Success, string? Error = null, UserDto? User = null, bool UsingDefaultPassword = false);

    /// <summary>Add (Id = 0) or edit a staff account. Password is only used when adding.</summary>
    public record SaveUserRequest(long Id, string Username, string Name, string? Position, UserRole Role,
                                  bool ActiveStatus, string? Password);

    /// <summary>Result of a save: OK, or the message to show the user.</summary>
    /// <param name="Id">The Id of the row that was added or changed, when the caller needs it.</param>
    public record ServiceResult(bool Success, string? Error = null, long Id = 0)
    {
        public static ServiceResult Ok(long id = 0) => new(true, Id: id);
        public static ServiceResult Fail(string error) => new(false, error);
    }
}
