using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class ChecklistTemplatesQueries(DB db, Logger logger) : CoreQueries<ChecklistTemplates>(db, logger)
    {
        public Task<List<ChecklistTemplates>> GetAllWithItemsAsync() =>
            Query.Include(t => t.Items).OrderBy(t => t.Name).ToListAsync();

        public Task<ChecklistTemplates?> GetWithItemsAsync(long id) =>
            Query.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);

        /// <summary>Tracked, with its items, so both can be changed and saved together.</summary>
        public Task<ChecklistTemplates?> GetForEditWithItemsAsync(long id) =>
            Set.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == id);

        public Task<bool> NameExistsAsync(string name, long excludeId = 0) =>
            AnyAsync(t => t.Name == name && t.Id != excludeId);

        /// <summary>How many encoded DVs use each template: template Id → count.</summary>
        public Task<Dictionary<long, int>> CountDvsByTemplateAsync() =>
            Db.Set<DisbursementVouchers>().AsNoTracking().Where(v => !v.IsDraft && v.ChecklistTemplateId != null)
              .GroupBy(v => v.ChecklistTemplateId!.Value).Select(g => new { g.Key, Count = g.Count() })
              .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
