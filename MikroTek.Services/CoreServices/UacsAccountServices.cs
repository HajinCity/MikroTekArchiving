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
    public class UacsAccountServices : CoreServices, IUacsAccounts
    {
        private static readonly Regex CodePattern = new(@"^\d{10}$");

        private readonly UacsAccountsQueries _accounts;

        public UacsAccountServices(DB db, Logger logger, UacsAccountsQueries accounts) : base(db, logger)
        {
            _accounts = accounts;
        }

        public async Task<List<UacsAccountDto>> GetAllAsync() =>
            (await _accounts.GetAllSortedAsync())
                .Select(a => new UacsAccountDto(a.Id, a.Code, a.Title, a.Group, a.IsActive)).ToList();

        public async Task<ServiceResult> SaveAsync(SaveUacsAccountRequest r, long byUserId)
        {
            // People often type or paste the code with spaces or dashes: 5-02-03-010-00.
            var code = Regex.Replace(r.Code ?? "", @"[\s-]", "");
            var title = Regex.Replace((r.Title ?? "").Trim(), @"\s+", " ");

            if (!CodePattern.IsMatch(code))
                return ServiceResult.Fail("The UACS code must be exactly 10 digits, for example 5020301000.");
            if (title.Length == 0 || title.Length > 150)
                return ServiceResult.Fail("Please enter the account title (up to 150 characters).");
            if (!Enum.IsDefined(r.Group))
                return ServiceResult.Fail("Please choose the account group.");
            if (await _accounts.CodeExistsAsync(code, r.Id))
                return ServiceResult.Fail($"UACS code {code} is already in the list.");

            _accounts.UserId = byUserId;
            if (r.Id == 0)
            {
                await _accounts.CreateAsync(new UacsAccounts { Code = code, Title = title, Group = r.Group, IsActive = r.IsActive });
                _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} added UACS account {code} {title}.");
                return ServiceResult.Ok();
            }

            var account = await _accounts.GetForEditAsync(r.Id);
            if (account is null) return ServiceResult.Fail("That account no longer exists.");

            account.Code = code;
            account.Title = title;
            account.Group = r.Group;
            account.IsActive = r.IsActive;
            await _accounts.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} updated UACS account {code} {title} (active={r.IsActive}).");
            return ServiceResult.Ok();
        }
    }
}
