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

            // The accounts the school already uses. More are added in Setup → UACS Accounts.
            if (!await db.UacsAccounts.AnyAsync())
            {
                (string Code, string Title, AccountGroup Group)[] accounts =
                [
                    ("1990101000", "Advances for Operating Expenses", AccountGroup.Asset),
                    ("1040502000", "Semi-Expendable Office Equipment (Inventory)", AccountGroup.Asset),
                    ("2020101000", "Due to BIR", AccountGroup.Liability),
                    ("5020101000", "Traveling Expenses - Local", AccountGroup.Expense),
                    ("5020301000", "Office Supplies Expenses", AccountGroup.Expense),
                    ("5020321002", "Semi-Expendable Office Equipment", AccountGroup.Expense),
                    ("5020321003", "Semi-Expendable ICT Equipment", AccountGroup.Expense),
                    ("5020321008", "Semi-Expendable DRR Equipment", AccountGroup.Expense),
                    ("5020399000", "Other Supplies and Materials Expenses", AccountGroup.Expense),
                    ("5020402000", "Electricity Expenses", AccountGroup.Expense),
                    ("5020503000", "Internet Subscription Expenses", AccountGroup.Expense),
                    ("5021299000", "Other General Services", AccountGroup.Expense),
                    ("5021304002", "Repair and Maintenance - School Buildings", AccountGroup.Expense),
                    ("5029904000", "Transportation and Delivery Expenses", AccountGroup.Expense)
                ];
                db.UacsAccounts.AddRange(accounts.Select(a => new UacsAccounts
                    { Code = a.Code, Title = a.Title, Group = a.Group, CreatedAt = DateTime.Now }));
            }

            // The school's own details for report headers. Signatories and bank accounts are filled in
            // by the admin in Setup → School & Signatories.
            if (!await db.SchoolProfiles.AnyAsync())
            {
                db.SchoolProfiles.Add(new SchoolProfiles
                {
                    SchoolName = "Leonardo National High School",
                    Region = "Region IX, Zamboanga Peninsula",
                    Division = "Division of Zamboanga del Sur",
                    District = "Josefina District",
                    Address = "Leonardo, Josefina, Zamboanga del Sur",
                    FundCluster = "Regular",
                    CreatedAt = DateTime.Now
                });
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
