using Microsoft.EntityFrameworkCore;
using MikroTek.Common;
using MikroTek.Domain;

namespace MikroTek.Services.DAL
{
    /// <summary>Starter data added the first time: the admin account, the plans and the TEST vouchers.</summary>
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

            await db.SaveChangesAsync();
        }
    }
}
