using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class SchoolProfileServices : CoreServices, ISchoolProfile
    {
        private readonly SchoolProfilesQueries _profiles;

        public SchoolProfileServices(DB db, Logger logger, SchoolProfilesQueries profiles) : base(db, logger)
        {
            _profiles = profiles;
        }

        public async Task<SchoolProfileDto> GetAsync()
        {
            var p = await _profiles.GetTheOneAsync();
            if (p is null) return new SchoolProfileDto();
            return new SchoolProfileDto
            {
                SchoolName = p.SchoolName, Region = p.Region, Division = p.Division, District = p.District, Address = p.Address,
                JhsBankAccount = p.JhsBankAccount, ShsBankAccount = p.ShsBankAccount,
                ResponsibilityCenter = p.ResponsibilityCenter, FundCluster = p.FundCluster,
                HeadName = p.HeadName, HeadPosition = p.HeadPosition,
                BookkeeperName = p.BookkeeperName, BookkeeperPosition = p.BookkeeperPosition,
                ValidatorName = p.ValidatorName, ValidatorPosition = p.ValidatorPosition,
                CashUnitName = p.CashUnitName, CashUnitPosition = p.CashUnitPosition
            };
        }

        public async Task<ServiceResult> SaveAsync(SchoolProfileDto r, long byUserId)
        {
            var name = Clean(r.SchoolName, 200);
            if (name is null) return ServiceResult.Fail("Please enter the school name.");

            _profiles.UserId = byUserId;
            var p = await _profiles.GetTheOneForEditAsync();
            if (p is null)
            {
                p = new SchoolProfiles();
                _profiles.Add(p);
            }

            p.SchoolName = name;
            p.Region = Clean(r.Region, 150);
            p.Division = Clean(r.Division, 150);
            p.District = Clean(r.District, 150);
            p.Address = Clean(r.Address, 250);
            p.JhsBankAccount = Clean(r.JhsBankAccount, 100);
            p.ShsBankAccount = Clean(r.ShsBankAccount, 100);
            p.ResponsibilityCenter = Clean(r.ResponsibilityCenter, 50);
            p.FundCluster = Clean(r.FundCluster, 50);
            p.HeadName = Clean(r.HeadName, 150);
            p.HeadPosition = Clean(r.HeadPosition, 150);
            p.BookkeeperName = Clean(r.BookkeeperName, 150);
            p.BookkeeperPosition = Clean(r.BookkeeperPosition, 150);
            p.ValidatorName = Clean(r.ValidatorName, 150);
            p.ValidatorPosition = Clean(r.ValidatorPosition, 150);
            p.CashUnitName = Clean(r.CashUnitName, 150);
            p.CashUnitPosition = Clean(r.CashUnitPosition, 150);
            await _profiles.SaveChangesAsync();

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} saved the school profile and signatories.");
            return ServiceResult.Ok();
        }

        /// <summary>Trimmed and cut to the column's size; empty becomes null.</summary>
        private static string? Clean(string? text, int max)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;
            return text.Length > max ? text[..max] : text;
        }
    }
}
