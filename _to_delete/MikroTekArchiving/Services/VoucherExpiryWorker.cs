namespace MikroTekArchiving.Services;

public class VoucherExpiryWorker(IServiceScopeFactory scopes, ILogger<VoucherExpiryWorker> logger)
    : BackgroundService
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
                var vouchers = scope.ServiceProvider.GetRequiredService<VoucherService>();
                var expired = await vouchers.ExpireDueAsync();
                if (expired > 0) logger.LogInformation("Expired {Count} voucher(s).", expired);
                if (failing) logger.LogInformation("Database is reachable again; voucher expiry resumed.");
                failing = false;
            }
            catch (Exception ex)
            {
                if (!failing) logger.LogError(ex, "Voucher expiry check failed. Retrying every 5 minutes.");
                failing = true;
                skipTicks = 9;
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
