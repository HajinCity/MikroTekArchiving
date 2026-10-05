using Microsoft.EntityFrameworkCore;
using MikroTekArchiving.Models.Entities;

namespace MikroTekArchiving.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<HotspotSession> HotspotSessions => Set<HotspotSession>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AdminUser>().HasIndex(a => a.Username).IsUnique();

        b.Entity<Plan>().Property(p => p.Price).HasPrecision(10, 2);

        b.Entity<Voucher>(v =>
        {
            v.HasIndex(x => x.Code).IsUnique();
            v.HasIndex(x => x.Status);
            // Store the enum as text ("Unused", "Active", ...) so the table is easy to read.
            v.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            v.HasOne(x => x.Plan).WithMany(p => p.Vouchers)
             .HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<HotspotSession>(s =>
        {
            s.HasIndex(x => new { x.MacAddress, x.LogoutAt });
            s.HasOne(x => x.Voucher).WithMany(v => v.Sessions)
             .HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
