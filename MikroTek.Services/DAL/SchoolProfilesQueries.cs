using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class SchoolProfilesQueries(DB db, Logger logger) : CoreQueries<SchoolProfiles>(db, logger)
    {
        /// <summary>The one school profile row (null only before the first start-up has added it).</summary>
        public Task<SchoolProfiles?> GetTheOneAsync() => Query.OrderBy(s => s.Id).FirstOrDefaultAsync();

        public Task<SchoolProfiles?> GetTheOneForEditAsync() => Set.OrderBy(s => s.Id).FirstOrDefaultAsync();
    }
}
