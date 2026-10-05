using System.Text.RegularExpressions;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class UserServices : CoreServices, IUsers
    {
        public const int MinPasswordLength = 8;
        private const string DefaultAdminUser = "admin", DefaultAdminPassword = "admin123";
        private static readonly Regex UsernamePattern = new("^[a-zA-Z0-9._-]{3,50}$");

        private readonly UsersQueries _users;

        public UserServices(DB db, Logger logger, UsersQueries users) : base(db, logger)
        {
            _users = users;
        }

        public async Task<SignInResult> SignInAsync(string? username, string? password)
        {
            const string wrong = "Incorrect username or password.";
            username = (username ?? "").Trim();
            if (username.Length == 0 || string.IsNullOrEmpty(password))
                return new SignInResult(false, "Please enter your username and password.");

            var user = await _users.GetByUsernameAsync(username);
            if (user is null || !Encryption.VerifyPassword(password, user.Password))
            {
                _logger.WriteLog(Level.Warning, LogType.Activity, $"Failed sign-in for '{username}'.");
                return new SignInResult(false, wrong);
            }
            if (!user.ActiveStatus)
                return new SignInResult(false, "This account has been disabled. Please ask the admin.");

            user.LastLoginTime = DateTime.Now;
            await _users.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"{user.Username} signed in.");

            var usingDefault = user.Username == DefaultAdminUser && password == DefaultAdminPassword;
            return new SignInResult(true, User: ToDto(user), UsingDefaultPassword: usingDefault);
        }

        public async Task SignOutAsync(long userId)
        {
            var user = await _users.GetForEditAsync(userId);
            if (user is null) return;
            user.LastLogoutTime = DateTime.Now;
            await _users.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"{user.Username} signed out.");
        }

        public async Task<UserDto?> GetAsync(long id) =>
            await _users.SelectByIdAsync(id) is { } u ? ToDto(u) : null;

        public async Task<List<UserDto>> GetAllAsync() =>
            (await _users.GetAllSortedAsync()).Select(ToDto).ToList();

        public async Task<ServiceResult> SaveAsync(SaveUserRequest r, long byUserId)
        {
            var username = (r.Username ?? "").Trim();
            var name = (r.Name ?? "").Trim();
            var position = string.IsNullOrWhiteSpace(r.Position) ? null : r.Position.Trim();

            if (!UsernamePattern.IsMatch(username))
                return ServiceResult.Fail("Username must be 3–50 characters: letters, numbers, dot, dash or underscore (no spaces).");
            if (name.Length == 0 || name.Length > 100)
                return ServiceResult.Fail("Please enter the full name (up to 100 characters).");
            if (position?.Length > 100)
                return ServiceResult.Fail("Position is too long (up to 100 characters).");
            if (await _users.UsernameExistsAsync(username, r.Id))
                return ServiceResult.Fail($"The username \"{username}\" is already taken.");

            if (r.Id == 0)
            {
                if (PasswordProblem(r.Password) is { } pw) return ServiceResult.Fail(pw);
                await _users.CreateAsync(new Users
                {
                    Username = username, Name = name, Position = position, Role = r.Role,
                    ActiveStatus = r.ActiveStatus, Password = Encryption.HashPassword(r.Password!)
                });
                _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} added staff account '{username}' ({r.Role}).");
                return ServiceResult.Ok();
            }

            var user = await _users.GetForEditAsync(r.Id);
            if (user is null) return ServiceResult.Fail("That staff account no longer exists.");

            var losesAdmin = user.Role == UserRole.Admin && user.ActiveStatus && (r.Role != UserRole.Admin || !r.ActiveStatus);
            if (losesAdmin && await _users.CountActiveAdminsAsync(excludeId: user.Id) == 0)
                return ServiceResult.Fail("There must always be at least one active Admin. Make another account Admin first.");
            if (user.Id == byUserId && !r.ActiveStatus)
                return ServiceResult.Fail("You can't disable your own account.");

            user.Username = username;
            user.Name = name;
            user.Position = position;
            user.Role = r.Role;
            user.ActiveStatus = r.ActiveStatus;
            await _users.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} updated staff account '{username}' ({r.Role}, active={r.ActiveStatus}).");
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> ResetPasswordAsync(long id, string? newPassword)
        {
            if (PasswordProblem(newPassword) is { } pw) return ServiceResult.Fail(pw);
            var user = await _users.GetForEditAsync(id);
            if (user is null) return ServiceResult.Fail("That staff account no longer exists.");

            user.Password = Encryption.HashPassword(newPassword!);
            await _users.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"Password reset for '{user.Username}'.");
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> ChangeOwnPasswordAsync(long id, string? currentPassword, string? newPassword)
        {
            var user = await _users.GetForEditAsync(id);
            if (user is null) return ServiceResult.Fail("Your account no longer exists.");
            if (string.IsNullOrEmpty(currentPassword) || !Encryption.VerifyPassword(currentPassword, user.Password))
                return ServiceResult.Fail("Your current password is not correct.");
            if (PasswordProblem(newPassword) is { } pw) return ServiceResult.Fail(pw);
            if (newPassword == currentPassword)
                return ServiceResult.Fail("The new password must be different from the current one.");

            user.Password = Encryption.HashPassword(newPassword!);
            await _users.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"{user.Username} changed their password.");
            return ServiceResult.Ok();
        }

        private static string? PasswordProblem(string? password) =>
            string.IsNullOrEmpty(password) || password.Length < MinPasswordLength
                ? $"The password must be at least {MinPasswordLength} characters."
                : null;

        private static UserDto ToDto(Users u) =>
            new(u.Id, u.Username, u.Name, u.Position, u.Role, u.ActiveStatus, u.LastLoginTime, u.LastLogoutTime);
    }
}
