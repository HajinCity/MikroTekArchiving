using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class PlansQueries(DB db, Logger logger) : CoreQueries<Plans>(db, logger)
    {
        public Task<List<Plans>> GetActivePlansAsync() =>
            Query.Where(p => p.IsActive).OrderBy(p => p.Duration).ToListAsync();
    }
}
