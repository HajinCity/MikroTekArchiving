using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MikroTek.Services.ENUM;
using MikroTek.Services.Interface;
using MikroTek.Services.Utils;

namespace MikroTek.Services.CoreServices
{
 
    public class VoucherExpiryWorker(IServiceScopeFactory scopes, Logger logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            var skipTicks = 0;
            var failing = false;
            do
            {
                // While the database is down, only retry every 5 minutes so the log stays readable.
                if (skipTicks > 0) { skipTicks--; continue; }
                try
                {
                    using var scope = scopes.CreateScope();
                    var vouchers = scope.ServiceProvider.GetRequiredService<IVouchers>();
                    var expired = await vouchers.ExpireDueAsync();
                    if (expired > 0) logger.WriteLog(Level.Info, LogType.Activity, $"Expired {expired} voucher(s).");
                    if (failing) logger.WriteLog(Level.Info, LogType.General, "Database is reachable again; voucher expiry resumed.");
                    failing = false;
                }
                catch (Exception ex)
                {
                    if (!failing) logger.WriteLog(Level.Error, LogType.Exception, "Voucher expiry check failed. Retrying every 5 minutes.", ex);
                    failing = true;
                    skipTicks = 9;
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
