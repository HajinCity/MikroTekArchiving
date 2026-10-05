using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DAL;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
    public class FundBudgetServices : CoreServices, IFundBudgets
    {
        private const decimal MaxAmount = 9_999_999_999.99m;

        /// <summary>The four funds, always in this order.</summary>
        public static readonly (Department Department, FundType FundType)[] Funds =
        [
            (Department.JHS, FundType.AnnualBudget), (Department.SHS, FundType.AnnualBudget),
            (Department.JHS, FundType.SpecialFunds), (Department.SHS, FundType.SpecialFunds)
        ];

        private readonly FundBudgetsQueries _budgets;

        public FundBudgetServices(DB db, Logger logger, FundBudgetsQueries budgets) : base(db, logger)
        {
            _budgets = budgets;
        }

        public async Task<List<FundBudgetDto>> GetForYearAsync(int year)
        {
            var saved = await _budgets.FindAsync(f => f.Year == year);
            return Funds.Select(f => new FundBudgetDto(year, f.Department, f.FundType,
                saved.FirstOrDefault(s => s.Department == f.Department && s.FundType == f.FundType)?.Amount ?? 0)).ToList();
        }

        public async Task<ServiceResult> SaveAsync(SaveFundBudgetsRequest r, long byUserId)
        {
            if (r.Year < 2000 || r.Year > 2100)
                return ServiceResult.Fail("Please choose a valid year.");

            var amounts = new[] { r.JhsAnnual, r.ShsAnnual, r.JhsSpecial, r.ShsSpecial };
            if (amounts.Any(a => a < 0 || a > MaxAmount))
                return ServiceResult.Fail("Budgets must be between 0 and 9,999,999,999.99.");

            _budgets.UserId = byUserId;
            var saved = await _budgets.GetByYearAsync(r.Year);
            for (var i = 0; i < Funds.Length; i++)
            {
                var (dept, fund) = Funds[i];
                var row = saved.FirstOrDefault(s => s.Department == dept && s.FundType == fund);
                if (row is null)
                    _budgets.Add(new FundBudgets { Year = r.Year, Department = dept, FundType = fund, Amount = amounts[i] });
                else
                    row.Amount = amounts[i];
            }
            await _budgets.SaveChangesAsync();

            _logger.WriteLog(Level.Info, LogType.Activity, $"User {byUserId} saved the {r.Year} fund budgets.");
            return ServiceResult.Ok();
        }
    }
}
