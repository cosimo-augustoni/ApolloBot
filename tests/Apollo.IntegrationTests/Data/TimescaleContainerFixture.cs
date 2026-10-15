using Apollo.AppHost;
using Apollo.Data;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Apollo.IntegrationTests.Data;

/// <summary>
/// One TimescaleDB container for all data tests. Each test gets its own database that does not exist yet, as the
/// MigrationService finds it on a fresh deploy. Replaced by the shared fixture from #7.
/// </summary>
public sealed class TimescaleContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder($"{TimescaleImage.Image}:{TimescaleImage.Tag}").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// A connection string for a database name no other test uses. The database itself is not created.
    /// </summary>
    public string NewDatabaseConnectionString() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "apollo_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;

    public static ApolloDbContext CreateContext(string connectionString) =>
        new(ApolloDbContext.CreateOptions(connectionString));
}
