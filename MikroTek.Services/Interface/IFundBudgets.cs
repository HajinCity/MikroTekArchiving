using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>Yearly budgets of the four funds: JHS/SHS × Annual Budget/Special Funds.</summary>
    public interface IFundBudgets
    {
        /// <summary>Always four rows in a fixed order (JHS Annual, SHS Annual, JHS Special, SHS Special); 0 when not set.</summary>
        Task<List<FundBudgetDto>> GetForYearAsync(int year);

        Task<ServiceResult> SaveAsync(SaveFundBudgetsRequest request, long byUserId);
    }
}
