using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class FundBudgetsQueries(DB db, Logger logger) : CoreQueries<FundBudgets>(db, logger)
    {
        /// <summary>The saved budgets of one year (tracked, so they can be changed and saved).</summary>
        public Task<List<FundBudgets>> GetByYearAsync(int year) =>
            Set.Where(f => f.Year == year).ToListAsync();
    }
}
