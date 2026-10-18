using Apollo.AppHost;
using Apollo.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
    /// An application builder with <see cref="ApolloDbContext"/> registered on the migrated database exactly as the
    /// services register it at runtime (<see cref="ApolloDataExtensions.AddApolloDbContext{TBuilder}"/>: Aspire's Npgsql
    /// integration with its settings and context pooling, plus the ID resolver). Use it for code whose behaviour depends
    /// on that registration, such as explicit transactions; <see cref="CreateDbContext"/> stays the choice for plain DB
    /// tests. Register test doubles (e.g. a <see cref="TimeProvider"/>) on <see cref="IHostApplicationBuilder.Services"/>.
    /// </summary>
    public HostApplicationBuilder CreateApplicationBuilder()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ApolloDbContext.ConnectionName}"] = ConnectionString,
        });
        builder.AddApolloDbContext();
        return builder;
    }

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

    /// <summary>
    /// Materializes every continuous aggregate over its whole range, as the refresh policies would over theirs. Call it
    /// after seeding, before asserting on an aggregate (coding-guidelines §4). Runs outside a transaction, which
    /// <c>refresh_continuous_aggregate</c> requires.
    /// </summary>
    public async Task RefreshContinuousAggregatesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var refreshes = new List<string>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT format('CALL refresh_continuous_aggregate(%L, NULL, NULL)', format('%I.%I', view_schema, view_name))
            FROM timescaledb_information.continuous_aggregates
            """,
            connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                refreshes.Add(reader.GetString(0));
            }
        }

        foreach (var refresh in refreshes)
        {
            await using var command = new NpgsqlCommand(refresh, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
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
