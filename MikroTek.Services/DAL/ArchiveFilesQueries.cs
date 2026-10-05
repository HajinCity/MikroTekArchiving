using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.DTO;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    public class ArchiveFilesQueries(DB db, Logger logger) : CoreQueries<ArchiveFiles>(db, logger)
    {
        /// <summary>The files of one voucher or cash advance, oldest first, with who uploaded each.</summary>
        public Task<List<ArchiveFileDto>> GetForOwnerAsync(FileOwner ownerType, long ownerId) =>
            (from f in Query
             where f.OwnerType == ownerType && f.OwnerId == ownerId
             join u in Db.Set<Users>() on f.CreatedBy equals (long?)u.Id into uploaders
             from u in uploaders.DefaultIfEmpty()
             orderby f.CreatedAt, f.Id
             select new ArchiveFileDto(f.Id, f.OwnerType, f.OwnerId, f.FileName, f.ContentType, f.SizeBytes, f.CreatedAt, u == null ? null : u.Name))
            .ToListAsync();

        public Task<List<ArchiveFiles>> GetByOwnerAsync(FileOwner ownerType, long ownerId) =>
            Query.Where(f => f.OwnerType == ownerType && f.OwnerId == ownerId).ToListAsync();

        /// <summary>How many files each record of this kind has: record Id → count.</summary>
        public Task<Dictionary<long, int>> CountByOwnerAsync(FileOwner ownerType) =>
            Query.Where(f => f.OwnerType == ownerType).GroupBy(f => f.OwnerId)
                 .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        /// <summary>Files attached to a voucher or cash advance that is no longer in the database.</summary>
        public Task<List<ArchiveFiles>> GetOrphansAsync() =>
            Query.Where(f => (f.OwnerType == FileOwner.Voucher && !Db.Set<DisbursementVouchers>().Any(v => v.Id == f.OwnerId))
                          || (f.OwnerType == FileOwner.CashAdvance && !Db.Set<CashAdvances>().Any(c => c.Id == f.OwnerId)))
                 .ToListAsync();
    }
}
