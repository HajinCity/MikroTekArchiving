using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MikroTekArchiving.Models.Entities;

namespace MikroTekArchiving.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await db.Database.MigrateAsync();

        if (!await db.AdminUsers.AnyAsync())
        {
            var admin = new AdminUser { Username = "admin", FullName = "System Administrator" };
            admin.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(admin, "admin123");
            db.AdminUsers.Add(admin);
        }

        if (!await db.Plans.AnyAsync())
        {
            var test = new Plan { Name = "2 Minutes (Test)", DurationMinutes = 2, Price = 0 };
            var hour = new Plan { Name = "1 Hour", DurationMinutes = 60, Price = 5 };
            var day = new Plan { Name = "1 Day", DurationMinutes = 1440, Price = 20 };
            var week = new Plan { Name = "7 Days", DurationMinutes = 10080, Price = 100 };
            db.Plans.AddRange(test, hour, day, week);

            db.Vouchers.AddRange(
                new Voucher { Code = "TEST2MIN", Plan = test, BatchNo = "DEMO", CreatedBy = "seed" },
                new Voucher { Code = "TEST1HR1", Plan = hour, BatchNo = "DEMO", CreatedBy = "seed" },
                new Voucher { Code = "TEST1HR2", Plan = hour, BatchNo = "DEMO", CreatedBy = "seed" },
                new Voucher { Code = "TEST1DAY", Plan = day, BatchNo = "DEMO", CreatedBy = "seed" },
                new Voucher { Code = "TESTWEEK", Plan = week, BatchNo = "DEMO", CreatedBy = "seed" },
                new Voucher { Code = "TESTDSBL", Plan = hour, BatchNo = "DEMO", CreatedBy = "seed",
                              Status = VoucherStatus.Disabled });
        }

        await db.SaveChangesAsync();
    }
}
