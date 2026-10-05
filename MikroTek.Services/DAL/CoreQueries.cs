using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MikroTek.Domain.Custom;
using MikroTek.Services.DTO;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    
    public abstract class CoreQueries<TEntity> : IGenericQuery<TEntity> where TEntity : BaseEntity, new()
    {
        protected CoreQueries(DbContext db, Logger logger)
        {
            Db = db;
            Set = db.Set<TEntity>();
            _logger = logger;
        }

        protected DbContext Db { get; }
        protected DbSet<TEntity> Set { get; }
        protected IQueryable<TEntity> Query => Set.AsNoTracking();
        protected readonly Logger _logger;       
        public long UserId { get; set; }
        private string _sql = string.Empty;
              
        public string Sql
        {
            get => _sql;
            protected set
            {
                _sql = value;
                _logger.WriteLog(Level.Debug, LogType.SQLStatement, _sql);
            }
        }      

        public virtual TEntity? SelectById(long id) =>
            Query.FirstOrDefault(e => e.Id == id);

        public virtual long Create(IEnumerable<TEntity> datas)
        {
            var list = datas.ToList();
            Set.AddRange(list);
            SaveAll();
            return list.Count;
        }

        public virtual long Create(TEntity data)
        {
            Set.Add(data);
            SaveAll();
            return data.Id;
        }

        public virtual int DeleteById(long id) =>
            Set.Where(e => e.Id == id).ExecuteDelete();

        public virtual bool Delete(TEntity data)
        {
            Set.Remove(data);
            return SaveAll() > 0;
        }

        public virtual bool Update(IEnumerable<TEntity> datas)
        {
            Set.UpdateRange(datas);
            return SaveAll() > 0;
        }

        public virtual bool Update(TEntity data)
        {
            Set.Update(data);
            return SaveAll() > 0;
        }

        public virtual Task<TEntity?> SelectByIdAsync(long id, CancellationToken ct = default) =>
            Query.FirstOrDefaultAsync(e => e.Id == id, ct);

        public virtual async Task<long> CreateAsync(IEnumerable<TEntity> datas, CancellationToken ct = default)
        {
            var list = datas.ToList();
            Set.AddRange(list);
            await SaveAllAsync(ct);
            return list.Count;
        }

        public virtual async Task<long> CreateAsync(TEntity data, CancellationToken ct = default)
        {
            Set.Add(data);
            await SaveAllAsync(ct);
            return data.Id;
        }

        public virtual Task<int> DeleteByIdAsync(long id, CancellationToken ct = default) =>
            Set.Where(e => e.Id == id).ExecuteDeleteAsync(ct);

        public virtual async Task<bool> DeleteAsync(TEntity data, CancellationToken ct = default)
        {
            Set.Remove(data);
            return await SaveAllAsync(ct) > 0;
        }

        public virtual async Task<bool> UpdateAsync(IEnumerable<TEntity> datas, CancellationToken ct = default)
        {
            Set.UpdateRange(datas);
            return await SaveAllAsync(ct) > 0;
        }

        public virtual async Task<bool> UpdateAsync(TEntity data, CancellationToken ct = default)
        {
            Set.Update(data);
            return await SaveAllAsync(ct) > 0;
        }       

        public virtual Task<List<TEntity>> GetAllAsync(CancellationToken ct = default) =>
            Query.ToListAsync(ct);       
        public virtual ValueTask<TEntity?> GetForEditAsync(long id, CancellationToken ct = default) =>
            Set.FindAsync([id], ct);

        public virtual Task<List<TEntity>> FindAsync(Expression<Func<TEntity, bool>> where, CancellationToken ct = default) =>
            Query.Where(where).ToListAsync(ct);

        public virtual Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> where, CancellationToken ct = default) =>
            Query.FirstOrDefaultAsync(where, ct);

        public virtual Task<bool> AnyAsync(Expression<Func<TEntity, bool>> where, CancellationToken ct = default) =>
            Set.AnyAsync(where, ct);

        public virtual Task<int> CountAsync(Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default) =>
            where is null ? Set.CountAsync(ct) : Set.CountAsync(where, ct);
    
        public virtual async Task<PagedResult<TEntity>> GetPageAsync(
            int page,
            int pageSize,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy,
            Expression<Func<TEntity, bool>>? where = null,
            CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var query = where is null ? Query : Query.Where(where);
            var total = await query.CountAsync(ct);
            var items = await orderBy(query).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return new PagedResult<TEntity>(items, page, pageSize, total);
        }
        public virtual void Add(TEntity entity) => Set.Add(entity);

        public virtual void AddRange(IEnumerable<TEntity> entities) => Set.AddRange(entities);
        public virtual void Remove(TEntity entity) => Set.Remove(entity);
        public virtual Task<int> SaveChangesAsync(CancellationToken ct = default) => SaveAllAsync(ct);      
        public virtual async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
        {
            await using var tx = await Db.Database.BeginTransactionAsync(ct);
            await work();
            await SaveAllAsync(ct);
            await tx.CommitAsync(ct);
        }
        protected IQueryable<TEntity> FromSql(FormattableString sql)
        {
            Sql = sql.Format;
            return Set.FromSql(sql).AsNoTracking();
        }

        protected Task<List<TResult>> SqlQueryAsync<TResult>(FormattableString sql, CancellationToken ct = default)
        {
            Sql = sql.Format;
            return Db.Database.SqlQuery<TResult>(sql).ToListAsync(ct);
        }

        protected Task<int> ExecuteSqlAsync(FormattableString sql, CancellationToken ct = default)
        {
            Sql = sql.Format;
            return Db.Database.ExecuteSqlAsync(sql, ct);
        }
        private int SaveAll()
        {
            StampAudit();
            try { return Db.SaveChanges(); }
            catch (Exception ex) { LogSaveError(ex); throw; }
        }

        private async Task<int> SaveAllAsync(CancellationToken ct)
        {
            StampAudit();
            try { return await Db.SaveChangesAsync(ct); }
            catch (Exception ex) { LogSaveError(ex); throw; }
        }      
        private void StampAudit()
        {
            var now = DateTime.Now;
            long? user = UserId == 0 ? null : UserId;

            foreach (var entry in Db.ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= user;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                }
            }
        }

        private void LogSaveError(Exception ex) =>
            _logger.WriteLog(Level.Error, LogType.Exception, $"Saving {typeof(TEntity).Name} failed: {ex.GetBaseException().Message}", ex);
    }
}
