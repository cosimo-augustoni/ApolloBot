using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apollo.Data;

public static class MigrationExtensions
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Waits until the MigrationService has applied every migration this build knows about, polling every
    /// <see cref="PollInterval"/>. Needed outside Development: Swarm has no start order, so bot and web may start
    /// before the database or the MigrationService is done.
    /// </summary>
    /// <exception cref="TimeoutException">Migrations are still pending after <see cref="DataOptions.MigrationWaitTimeout"/>.</exception>
    public static async Task WaitForMigrationsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var timeout = services.GetRequiredService<IOptions<DataOptions>>().Value.MigrationWaitTimeout;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MigrationExtensions).FullName!);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApolloDbContext>();

        var started = timeProvider.GetTimestamp();
        Exception? lastError = null;

        while (true)
        {
            try
            {
                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count == 0)
                {
                    logger.LogInformation("Database is up to date");
                    return;
                }

                logger.LogInformation("Waiting for {PendingCount} pending migrations: {Migrations}", pending.Count, pending);
            }
            catch (Exception exception) when (DatabaseErrors.IsDatabaseError(exception))
            {
                // Not reachable yet, or the MigrationService has not created the database yet.
                lastError = exception;
                logger.LogInformation("Waiting for the database: {Error}", exception.GetBaseException().Message);
            }

            if (timeProvider.GetElapsedTime(started) >= timeout)
            {
                throw new TimeoutException(
                    $"Migrations were still pending after {timeout}. Check the migrationservice logs.", lastError);
            }

            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }
    }
}
