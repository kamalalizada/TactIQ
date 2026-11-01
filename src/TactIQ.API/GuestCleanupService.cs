using TactIQ.Infrastructure.Identity;

namespace TactIQ.API;

/// <summary>
/// Clears guests who have not come back. It also runs on boot, because a free
/// host sleeps when nobody visits and a sleeping process has no timer to fire.
/// </summary>
public class GuestCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<GuestCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var guests = scope.ServiceProvider.GetRequiredService<IGuestService>();

                var deleted = await guests.DeleteExpiredAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Deleted {Count} expired guest account(s)", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed sweep is retried next time; it must not stop the API.
                logger.LogError(ex, "Guest cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
