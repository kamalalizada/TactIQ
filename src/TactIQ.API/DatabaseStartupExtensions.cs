using Microsoft.EntityFrameworkCore;
using TactIQ.Infrastructure.Persistence;

namespace TactIQ.API;

public static class DatabaseStartupExtensions
{
    /// <summary>Applies any pending EF Core migrations so a fresh clone just needs `dotnet run`.</summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseStartupExtensions));

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pending.Count == 0)
        {
            logger.LogInformation("Database schema is up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrated.");
    }
}
