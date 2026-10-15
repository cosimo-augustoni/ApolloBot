using System.Diagnostics;
using Apollo.Data;

namespace Apollo.MigrationService;

/// <summary>
/// Applies all pending migrations once, then stops the process. Exit code 0 on success, 1 on failure, so Aspire
/// shows Finished/0 and Swarm's task state is Complete or Failed (docs/deployment.md).
/// </summary>
public sealed class MigrationWorker(
    IServiceProvider services,
    DatabaseMigrator migrator,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    public const string ActivitySourceName = "Apollo.MigrationService";

    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = _activitySource.StartActivity("Migrating database", ActivityKind.Client);

        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApolloDbContext>();

            var applied = await migrator.MigrateAsync(db, stoppingToken);
            logger.LogInformation("Database migrated, {AppliedCount} migrations applied", applied.Count);
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogCritical(exception, "Database migration failed");
            Environment.ExitCode = 1;
        }

        lifetime.StopApplication();
    }
}
