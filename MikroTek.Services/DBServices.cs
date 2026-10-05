using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MikroTek.Common;
using MikroTek.Services.CoreServices;
using MikroTek.Services.DAL;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services
{    
    public static class DBServices
    {
        /// <param name="hotspotDemo">false on the school server: the voucher expiry job is not started.</param>
        public static IServiceCollection AddMikroTekServices(this IServiceCollection services, string connectionString, bool hotspotDemo = true)
        {
            services.AddDbContext<DB>(options => options.UseSqlServer(connectionString));
            services.AddSingleton<Logger>();           
            services.AddScoped<UsersQueries>();
            services.AddScoped<PlansQueries>();
            services.AddScoped<VouchersQueries>();
            services.AddScoped<HotspotSessionsQueries>();
            services.AddScoped<FundBudgetsQueries>();
            services.AddScoped<RolePermissionsQueries>();       
            services.AddScoped<IVouchers, VoucherServices>();
            services.AddScoped<IUsers, UserServices>();
            services.AddScoped<IFundBudgets, FundBudgetServices>();
            services.AddScoped<IRoles, RoleServices>();         
            services.AddSingleton<IRouterService, FakeRouterService>();

            
            if (hotspotDemo)
                services.AddHostedService<VoucherExpiryWorker>();
            return services;
        }

      
        public static async Task InitializeDatabaseAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DB>();
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(db);
        }
    }
}
