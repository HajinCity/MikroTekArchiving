using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class CashAdvanceServices : CoreServices, ICashAdvances
    {
        private const decimal MaxAmount = 9_999_999_999.99m;

        private readonly CashAdvancesQueries _advances;
        private readonly ArchiveLoader _loader;
        private readonly IArchiveFiles _files;

        public CashAdvanceServices(DB db, Logger logger, CashAdvancesQueries advances, ArchiveLoader loader, IArchiveFiles files)
            : base(db, logger)
        {
            _advances = advances;
            _loader = loader;
            _files = files;
        }

        public Task<List<CashAdvanceDto>> GetAllAsync() => _loader.GetCashAdvancesAsync();

        public async Task<CashAdvanceDto?> GetAsync(long id) => (await _loader.GetCashAdvancesAsync(id)).FirstOrDefault();

        public async Task<ServiceResult> SaveAsync(SaveCashAdvanceRequest r, long byUserId)
        {
            var payrollNo = (r.PayrollNo ?? "").Trim();

            if (!Enum.IsDefined(r.Dept) || !Enum.IsDefined(r.Fund))
                return ServiceResult.Fail("Please choose the department and the fund.");
            if (r.BatchNo is < 1 or > 99)
                return ServiceResult.Fail("The batch number must be between 1 and 99.");
            if (payrollNo.Length == 0 || payrollNo.Length > 50)
                return ServiceResult.Fail("Please enter the Payroll No. from the MOOE slip (up to 50 characters).");
            if (r.Date is null || r.Date.Value.Year is < 2000 or > 2100)
                return ServiceResult.Fail("Please enter the date of the cash advance.");
            if (r.Amount is null or <= 0 or > MaxAmount)
                return ServiceResult.Fail("Please enter the amount of the cash advance (more than 0).");
            if (await _advances.PayrollNoExistsAsync(payrollNo, r.Id))
                return ServiceResult.Fail($"Payroll No. {payrollNo} is already recorded.");

            _advances.UserId = byUserId;
            CashAdvances ca;
            if (r.Id == 0)
            {
                ca = new CashAdvances();
                _advances.Add(ca);
            }
            else
            {
                var existing = await _advances.GetForEditAsync(r.Id);
                if (existing is null) return ServiceResult.Fail("That cash advance no longer exists.");
                ca = existing;
            }

            ca.Department = r.Dept;
            ca.FundType = r.Fund;
            ca.BatchNo = r.BatchNo;
            ca.PayrollNo = payrollNo;
            ca.AdaNo = Clean(r.AdaNo, 50);
            ca.Date = r.Date.Value.Date;
            ca.Amount = r.Amount.Value;
            ca.Period = Clean(r.Period, 100);
            ca.FundCluster = Clean(r.FundCluster, 50);
            ca.Officer = Clean(r.Officer, 150);
            ca.OfficerPosition = Clean(r.OfficerPosition, 150);
            ca.VerifiedBy = Clean(r.VerifiedBy, 150);
            ca.VerifiedByPosition = Clean(r.VerifiedByPosition, 150);
            await _advances.SaveChangesAsync();

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} saved cash advance {payrollNo} ({ca.Department}, {ca.Amount:N2}).");
            return ServiceResult.Ok(ca.Id);
        }

        public async Task<ServiceResult> SetLiquidatedAsync(long id, bool liquidated, long byUserId)
        {
            _advances.UserId = byUserId;
            var ca = await _advances.GetForEditAsync(id);
            if (ca is null) return ServiceResult.Fail("That cash advance no longer exists.");

            ca.Status = liquidated ? CashAdvanceStatus.Liquidated : CashAdvanceStatus.Open;
            await _advances.SaveChangesAsync();
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} marked cash advance {ca.PayrollNo} as {ca.Status}.");
            return ServiceResult.Ok(id);
        }

        public async Task<ServiceResult> DeleteAsync(long id, long byUserId)
        {
            var ca = await _advances.SelectByIdAsync(id);
            if (ca is null) return ServiceResult.Fail("That cash advance no longer exists.");
            if (await _advances.CountVouchersAsync(id) > 0)
                return ServiceResult.Fail("This cash advance has DVs charged to it. Delete or move those DVs first.");

            await _files.DeleteForOwnerAsync(FileOwner.CashAdvance, id);
            await _advances.DeleteByIdAsync(id);
            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} deleted cash advance {ca.PayrollNo}.");
            return ServiceResult.Ok();
        }

        private static string? Clean(string? text, int max)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;
            return text.Length > max ? text[..max] : text;
        }
    }
}
