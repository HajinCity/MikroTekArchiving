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
        public DbSet<Payees> Payees => Set<Payees>();
        public DbSet<ChecklistTemplates> ChecklistTemplates => Set<ChecklistTemplates>();
        public DbSet<ChecklistTemplateItems> ChecklistTemplateItems => Set<ChecklistTemplateItems>();
        public DbSet<SchoolProfiles> SchoolProfiles => Set<SchoolProfiles>();
        public DbSet<CashAdvances> CashAdvances => Set<CashAdvances>();
        public DbSet<DisbursementVouchers> DisbursementVouchers => Set<DisbursementVouchers>();
        public DbSet<DvLines> DvLines => Set<DvLines>();
        public DbSet<DvChecklistItems> DvChecklistItems => Set<DvChecklistItems>();

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

            b.Entity<Payees>(p =>
            {
                p.Property(x => x.Name).HasMaxLength(200).IsRequired();
                p.HasIndex(x => x.Name).IsUnique();
                p.Property(x => x.Tin).HasMaxLength(30);
                p.Property(x => x.Address).HasMaxLength(250);
                p.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
                p.Property(x => x.TaxType).HasConversion<string>().HasMaxLength(20);
            });

            b.Entity<ChecklistTemplates>(t =>
            {
                t.Property(x => x.Name).HasMaxLength(200).IsRequired();
                t.HasIndex(x => x.Name).IsUnique();
                t.HasMany(x => x.Items).WithOne(i => i.Template)
                 .HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<ChecklistTemplateItems>(i => i.Property(x => x.Label).HasMaxLength(300).IsRequired());

            b.Entity<SchoolProfiles>(s =>
            {
                s.Property(x => x.SchoolName).HasMaxLength(200).IsRequired();
                s.Property(x => x.Region).HasMaxLength(150);
                s.Property(x => x.Division).HasMaxLength(150);
                s.Property(x => x.District).HasMaxLength(150);
                s.Property(x => x.Address).HasMaxLength(250);
                s.Property(x => x.JhsBankAccount).HasMaxLength(100);
                s.Property(x => x.ShsBankAccount).HasMaxLength(100);
                s.Property(x => x.ResponsibilityCenter).HasMaxLength(50);
                s.Property(x => x.FundCluster).HasMaxLength(50);
                s.Property(x => x.HeadName).HasMaxLength(150);
                s.Property(x => x.HeadPosition).HasMaxLength(150);
                s.Property(x => x.BookkeeperName).HasMaxLength(150);
                s.Property(x => x.BookkeeperPosition).HasMaxLength(150);
                s.Property(x => x.ValidatorName).HasMaxLength(150);
                s.Property(x => x.ValidatorPosition).HasMaxLength(150);
                s.Property(x => x.CashUnitName).HasMaxLength(150);
                s.Property(x => x.CashUnitPosition).HasMaxLength(150);
            });

            b.Entity<CashAdvances>(c =>
            {
                c.Property(x => x.Department).HasConversion<string>().HasMaxLength(10);
                c.Property(x => x.FundType).HasConversion<string>().HasMaxLength(20);
                c.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
                c.Property(x => x.PayrollNo).HasMaxLength(50).IsRequired();
                c.HasIndex(x => x.PayrollNo).IsUnique();
                c.Property(x => x.AdaNo).HasMaxLength(50);
                c.Property(x => x.Date).HasColumnType("date");
                c.Property(x => x.Amount).HasPrecision(18, 2);
                c.Property(x => x.Period).HasMaxLength(100);
                c.Property(x => x.FundCluster).HasMaxLength(50);
                c.Property(x => x.Officer).HasMaxLength(150);
                c.Property(x => x.OfficerPosition).HasMaxLength(150);
                c.Property(x => x.VerifiedBy).HasMaxLength(150);
                c.Property(x => x.VerifiedByPosition).HasMaxLength(150);
            });

            b.Entity<DisbursementVouchers>(v =>
            {
                v.Property(x => x.DvNo).HasMaxLength(30);
                v.HasIndex(x => x.DvNo).IsUnique();
                v.Property(x => x.Date).HasColumnType("date");
                v.Property(x => x.PaidDate).HasColumnType("date");
                v.Property(x => x.Type).HasMaxLength(200);
                v.Property(x => x.Payee).HasMaxLength(200);
                v.Property(x => x.Tin).HasMaxLength(30);
                v.Property(x => x.Address).HasMaxLength(250);
                v.Property(x => x.Particulars).HasMaxLength(1000);
                v.Property(x => x.FundCluster).HasMaxLength(50);
                v.Property(x => x.ResponsibilityCenter).HasMaxLength(50);
                v.Property(x => x.MfoPap).HasMaxLength(50);
                v.Property(x => x.OrsBurs).HasMaxLength(50);
                v.Property(x => x.Tax1).HasPrecision(18, 2);
                v.Property(x => x.Tax3).HasPrecision(18, 2);
                v.Property(x => x.Tax5).HasPrecision(18, 2);
                v.Property(x => x.TaxBase).HasPrecision(18, 2);
                v.Property(x => x.PayeeTaxType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(PayeeTaxType.NonVat);
                v.Property(x => x.Mode).HasMaxLength(30);
                v.Property(x => x.CheckNo).HasMaxLength(30);
                v.Property(x => x.Bank).HasMaxLength(100);
                v.Property(x => x.ReceivedBy).HasMaxLength(150);
                v.Property(x => x.OrNo).HasMaxLength(50);
                v.Property(x => x.JevNo).HasMaxLength(50);
                v.Property(x => x.ChecklistRemarks).HasMaxLength(500);
                // A cash advance that has DVs cannot be deleted by accident.
                v.HasOne(x => x.CashAdvance).WithMany(c => c.Vouchers)
                 .HasForeignKey(x => x.CashAdvanceId).OnDelete(DeleteBehavior.Restrict);
                v.HasMany(x => x.Lines).WithOne(l => l.Dv)
                 .HasForeignKey(l => l.DvId).OnDelete(DeleteBehavior.Cascade);
                v.HasMany(x => x.Checklist).WithOne(c => c.Dv)
                 .HasForeignKey(c => c.DvId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<DvLines>(l =>
            {
                l.Property(x => x.Code).HasMaxLength(10).IsRequired();
                l.Property(x => x.Title).HasMaxLength(150).IsRequired();
                l.Property(x => x.Amount).HasPrecision(18, 2);
            });

            b.Entity<DvChecklistItems>(c =>
            {
                c.Property(x => x.Label).HasMaxLength(300).IsRequired();
                c.Property(x => x.Mark).HasConversion<string>().HasMaxLength(20);
                c.Property(x => x.DateText).HasMaxLength(20);
                c.Property(x => x.Remarks).HasMaxLength(200);
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
