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
    }
}
