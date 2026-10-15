using Apollo.AppHost;
using Apollo.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Xunit;

namespace Apollo.Testing.Data;

/// <summary>
/// One TimescaleDB container per test collection, running the AppHost's image, with all <see cref="ApolloDbContext"/>
/// migrations applied once. Test classes opt in by deriving from <see cref="TimescaleTest"/>, which resets the
/// database before every test.
/// </summary>
public sealed class TimescaleFixture : IAsyncLifetime
{
    private static readonly RespawnerOptions _respawnerOptions = new()
    {
        DbAdapter = DbAdapter.Postgres,
        SchemasToInclude = ["public"],
        TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
    };

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).Build();

    /// <summary>
    /// The container image, shared with the AppHost through the linked <c>TimescaleImage.cs</c>.
    /// </summary>
    public static string Image => $"{TimescaleImage.Image}:{TimescaleImage.Tag}";

    /// <summary>
    /// The migrations applied when the collection started.
    /// </summary>
    public IReadOnlyList<string> AppliedMigrations { get; private set; } = [];

    /// <summary>
    /// The migrated database.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDbContext();
        AppliedMigrations = await new DatabaseMigrator(TimeProvider.System, NullLogger<DatabaseMigrator>.Instance)
            .MigrateAsync(db, CancellationToken.None);
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// A context on the migrated database. The caller disposes it.
    /// </summary>
    public ApolloDbContext CreateDbContext() => new(ApolloDbContext.CreateOptions(ConnectionString));

    /// <summary>
    /// A connection string for a database on the same server that does not exist yet, as the MigrationService finds it
    /// on a fresh deploy. For tests that need an unmigrated database.
    /// </summary>
    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = "apollo_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;

    /// <summary>
    /// Deletes all rows in schema <c>public</c>, except the migration history. The schema, the
    /// <c>timescaledb</c> extension and Timescale's internal schemas stay. Continuous aggregates are not reset;
    /// refresh them explicitly after seeding.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Respawn throws when it finds no table to reset. The respawner is built per reset, so tables a test creates
        // (or a later migration adds) are always included.
        if (!await HasTablesToResetAsync(connection, cancellationToken))
        {
            return;
        }

        var respawner = await Respawner.CreateAsync(connection, _respawnerOptions);
        await respawner.ResetAsync(connection);
    }

    private static async Task<bool> HasTablesToResetAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory')
            """,
            connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
