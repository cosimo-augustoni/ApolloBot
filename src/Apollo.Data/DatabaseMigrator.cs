using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Apollo.Data;

/// <summary>
/// Applies pending migrations, retrying while the database is unreachable (e.g. still starting in Swarm, where
/// nothing orders the services).
/// </summary>
public sealed class DatabaseMigrator(TimeProvider timeProvider, ILogger<DatabaseMigrator> logger)
{
    public const int MaxAttempts = 10;

    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <returns>The migrations that were applied, oldest first.</returns>
    public async Task<IReadOnlyList<string>> MigrateAsync(ApolloDbContext db, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await MigrateOnceAsync(db, cancellationToken);
            }
            catch (Exception exception) when (attempt < MaxAttempts && DatabaseErrors.IsTransient(exception))
            {
                logger.LogWarning(
                    exception,
                    "Database unreachable on attempt {Attempt} of {MaxAttempts}, retrying in {RetryDelay}",
                    attempt,
                    MaxAttempts,
                    RetryDelay);

                await Task.Delay(RetryDelay, timeProvider, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<string>> MigrateOnceAsync(ApolloDbContext db, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        // No transaction of our own around MigrateAsync: CmdScale runs continuous-aggregate statements outside one
        // (docs/adr/0001-timescale-ef.md), and EF already wraps each migration where it can.
        return await strategy.ExecuteAsync(async () =>
        {
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            logger.LogInformation("Applying {PendingCount} pending migrations: {Migrations}", pending.Count, pending);

            await db.Database.MigrateAsync(cancellationToken);
            return (IReadOnlyList<string>)pending;
        });
    }
}
