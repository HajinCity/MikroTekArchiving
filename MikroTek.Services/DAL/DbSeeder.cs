using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;
using MikroTek.Services.Utils;

namespace MikroTek.Services.DAL
{
    /// <summary>Starter data added the first time: the admin account, the plans, the TEST vouchers and each role's access.</summary>
    public static class DbSeeder
    {
        public static async Task SeedAsync(DB db)
        {
            if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
            {
                db.Users.Add(new Users
                {
                    Username = "admin",
                    Password = Encryption.HashPassword("admin123"),
                    Name = "System Administrator",
                    Role = UserRole.Admin
                });
            }

            if (!await db.Plans.AnyAsync())
            {
                var test = new Plans { Name = "2 Minutes (Test)", Duration = 2, PlanPrice = 0 };
                var hour = new Plans { Name = "1 Hour", Duration = 60, PlanPrice = 5 };
                var day = new Plans { Name = "1 Day", Duration = 1440, PlanPrice = 20 };
                var week = new Plans { Name = "7 Days", Duration = 10080, PlanPrice = 100 };
                db.Plans.AddRange(test, hour, day, week);

                // Fixed, easy-to-type codes so the hotspot login can be tested right away.
                db.Vouchers.AddRange(
                    new Vouchers { VoucherCode = "TEST2MIN", Plan = test, BatchNo = "DEMO", CreatedBy = "seed" },
                    new Vouchers { VoucherCode = "TEST1HR1", Plan = hour, BatchNo = "DEMO", CreatedBy = "seed" },
                    new Vouchers { VoucherCode = "TEST1HR2", Plan = hour, BatchNo = "DEMO", CreatedBy = "seed" },
                    new Vouchers { VoucherCode = "TEST1DAY", Plan = day, BatchNo = "DEMO", CreatedBy = "seed" },
                    new Vouchers { VoucherCode = "TESTWEEK", Plan = week, BatchNo = "DEMO", CreatedBy = "seed" },
                    new Vouchers { VoucherCode = "TESTDSBL", Plan = hour, BatchNo = "DEMO", CreatedBy = "seed",
                                   Status = VoucherStatus.Disabled });
            }

            // The starting ticks of the Roles & Access page. Only added once: later changes are the admin's.
            if (!await db.RolePermissions.AnyAsync())
            {
                foreach (var role in Enum.GetValues<UserRole>())
                    db.RolePermissions.AddRange(Permissions.DefaultsFor(role)
                        .Select(p => new RolePermissions { Role = role, Permission = p, CreatedAt = DateTime.Now }));
            }

            await db.SaveChangesAsync();
        }
    }
}
