using Apollo.AppHost;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Apollo.Spikes.TimescaleEf;

/// <summary>
/// One TimescaleDB container per test class. Migrates a database that does not exist yet (as the MigrationService will),
/// seeds the boundary events and refreshes both aggregates once.
/// </summary>
public sealed class TimescaleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder($"{TimescaleImage.Image}:{TimescaleImage.Tag}").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public SpikeDbContext CreateContext() => new(SpikeDbContext.CreateOptions(ConnectionString));

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "apollo_spike",
        }.ConnectionString;

        await using SpikeDbContext db = CreateContext();
        await db.Database.MigrateAsync();

        db.ActivityEvents.AddRange(SeedPoints.All.Select(p => new ActivityEvent
        {
            ChannelId = p.ChannelId,
            OccurredAt = p.OccurredAt,
        }));
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("CALL refresh_continuous_aggregate('daily_activity_zurich', NULL, NULL);");
        await db.Database.ExecuteSqlRawAsync("CALL refresh_continuous_aggregate('daily_activity_utc', NULL, NULL);");
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
