using Microsoft.EntityFrameworkCore;
using MikroTek.Domain;

namespace MikroTek.Common
{
   
    public class DB : DbContext
    {
        public DB(DbContextOptions<DB> options) : base(options) { }

        public DbSet<Users> Users => Set<Users>();
        public DbSet<Plans> Plans => Set<Plans>();
        public DbSet<Vouchers> Vouchers => Set<Vouchers>();
        public DbSet<HotspotSessions> HotspotSessions => Set<HotspotSessions>();
        public DbSet<FundBudgets> FundBudgets => Set<FundBudgets>();
        public DbSet<RolePermissions> RolePermissions => Set<RolePermissions>();
        public DbSet<UacsAccounts> UacsAccounts => Set<UacsAccounts>();
        public DbSet<ArchiveFiles> ArchiveFiles => Set<ArchiveFiles>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Users>(u =>
            {
                u.Property(x => x.Username).HasMaxLength(50).IsRequired();
                u.HasIndex(x => x.Username).IsUnique();
                u.Property(x => x.Password).HasMaxLength(255).IsRequired();
                u.Property(x => x.Name).HasMaxLength(100).IsRequired();
                u.Property(x => x.Position).HasMaxLength(100);
                u.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
                u.Property(x => x.LastLoginTime).HasColumnType("datetime2(0)");
                u.Property(x => x.LastLogoutTime).HasColumnType("datetime2(0)");
            });

            b.Entity<FundBudgets>(f =>
            {
                f.Property(x => x.Department).HasConversion<string>().HasMaxLength(10);
                f.Property(x => x.FundType).HasConversion<string>().HasMaxLength(20);
                f.Property(x => x.Amount).HasPrecision(18, 2);
                f.HasIndex(x => new { x.Year, x.Department, x.FundType }).IsUnique();
            });

            b.Entity<RolePermissions>(r =>
            {
                r.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
                r.Property(x => x.Permission).HasMaxLength(50).IsRequired();
                r.HasIndex(x => new { x.Role, x.Permission }).IsUnique();
            });

            b.Entity<UacsAccounts>(a =>
            {
                a.Property(x => x.Code).HasMaxLength(10).IsRequired();
                a.HasIndex(x => x.Code).IsUnique();
                a.Property(x => x.Title).HasMaxLength(150).IsRequired();
                a.Property(x => x.Group).HasConversion<string>().HasMaxLength(20);
            });

            b.Entity<ArchiveFiles>(f =>
            {
                f.Property(x => x.FileName).HasMaxLength(255).IsRequired();
                f.Property(x => x.StoredName).HasMaxLength(100).IsRequired();
                f.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
                f.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20);
                f.HasIndex(x => new { x.OwnerType, x.OwnerId });
            });

            b.Entity<Plans>(p =>
            {
                p.Property(x => x.Name).HasMaxLength(50).IsRequired();
                p.Property(x => x.PlanPrice).HasPrecision(10, 2);
            });

            b.Entity<Vouchers>(v =>
            {
                v.Property(x => x.VoucherCode).HasMaxLength(20).IsRequired();
                v.HasIndex(x => x.VoucherCode).IsUnique();
                // Stored as text ("Unused", "Active", ...) so the table is easy to read in SSMS.
                v.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                v.HasIndex(x => x.Status);
                v.Property(x => x.BatchNo).HasMaxLength(30);
                v.Property(x => x.CreatedBy).HasMaxLength(50);
                v.Property(x => x.MacAddress).HasMaxLength(17);
                v.HasOne(x => x.Plan).WithMany(p => p.Vouchers)
                 .HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            });

            b.Entity<HotspotSessions>(s =>
            {
                s.Property(x => x.MacAddress).HasMaxLength(17).IsRequired();
                s.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
                s.Property(x => x.EndReason).HasMaxLength(50);
                s.HasIndex(x => new { x.MacAddress, x.LogoutAt });
                s.HasOne(x => x.Voucher).WithMany(v => v.Sessions)
                 .HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
