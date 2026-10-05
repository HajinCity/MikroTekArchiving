using MikroTek.Domain;

namespace MikroTek.Services.DTO
{
    /// <summary>A staff account as the pages see it (never includes the password).</summary>
    public record UserDto(long Id, string Username, string Name, string? Position, UserRole Role,
                          bool ActiveStatus, DateTime? LastLoginTime, DateTime? LastLogoutTime)
    {
        public string Initials =>
            string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => char.IsLetter(p[0])).Take(2).Select(p => char.ToUpper(p[0])));
    }

    public record SignInResult(bool Success, string? Error = null, UserDto? User = null, bool UsingDefaultPassword = false);

    /// <summary>Add (Id = 0) or edit a staff account. Password is only used when adding.</summary>
    public record SaveUserRequest(long Id, string Username, string Name, string? Position, UserRole Role,
                                  bool ActiveStatus, string? Password);

    /// <summary>Result of a save: OK, or the message to show the user.</summary>
    public record ServiceResult(bool Success, string? Error = null)
    {
        public static ServiceResult Ok() => new(true);
        public static ServiceResult Fail(string error) => new(false, error);
    }
}
