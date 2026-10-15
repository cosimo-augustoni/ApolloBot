using System.Net.Sockets;
using Apollo.Data;
using Apollo.Testing;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Apollo.IntegrationTests.Data;

[Collection<TimescaleCollection>]
public sealed class DatabaseMigratorTests(TimescaleFixture fixture)
{
    private readonly FakeTimeProvider _time = new();

    private DatabaseMigrator Migrator => new(_time, NullLogger<DatabaseMigrator>.Instance);

    [Fact]
    public async Task MigrateAsync_WhenDatabaseEmpty_ShouldApplyAllMigrations()
    {
        await using var db = CreateContext(fixture.NewDatabaseConnectionString());

        var applied = await Migrator.MigrateAsync(db, TestContext.Current.CancellationToken);

        applied.ShouldBe(db.Database.GetMigrations().ToList());
        (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
            .ShouldContain(m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MigrateAsync_WhenDatabaseEmpty_ShouldInstallTimescaleExtension()
    {
        var connectionString = fixture.NewDatabaseConnectionString();
        await using var db = CreateContext(connectionString);

        await Migrator.MigrateAsync(db, TestContext.Current.CancellationToken);

        (await ScalarAsync(connectionString, "SELECT extversion FROM pg_extension WHERE extname = 'timescaledb'"))
            .ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MigrateAsync_WhenAlreadyMigrated_ShouldSucceedWithoutChanges()
    {
        await using var db = fixture.CreateDbContext();

        var applied = await Migrator.MigrateAsync(db, TestContext.Current.CancellationToken);

        applied.ShouldBeEmpty();
        (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
            .ShouldBe(db.Database.GetMigrations());
    }

    [Fact]
    public async Task MigrateAsync_WhenDatabaseUnavailableThenAvailable_ShouldRetryAndSucceed()
    {
        var unavailable = new UnavailableDatabaseInterceptor(failures: 2, transient: true);
        await using var db = CreateContext(fixture.NewDatabaseConnectionString(), unavailable);

        var applied = await RunAdvancingTimeAsync(Migrator.MigrateAsync(db, TestContext.Current.CancellationToken));

        unavailable.Attempts.ShouldBeGreaterThan(2);
        applied.ShouldBe(db.Database.GetMigrations().ToList());
    }

    [Fact]
    public async Task MigrateAsync_WhenDatabaseStaysUnavailable_ShouldThrowAfterMaxAttempts()
    {
        var unavailable = new UnavailableDatabaseInterceptor(failures: int.MaxValue, transient: true);
        await using var db = CreateContext(fixture.NewDatabaseConnectionString(), unavailable);

        // EF may wrap the provider error ("transient failure" InvalidOperationException), so check the root cause.
        var exception = await Should.ThrowAsync<Exception>(
            RunAdvancingTimeAsync(Migrator.MigrateAsync(db, TestContext.Current.CancellationToken)));

        exception.GetBaseException().ShouldBeOfType<SocketException>();
        unavailable.Attempts.ShouldBe(DatabaseMigrator.MaxAttempts);
    }

    [Fact]
    public async Task MigrateAsync_WhenFailureIsNotTransient_ShouldThrowWithoutRetrying()
    {
        var broken = new UnavailableDatabaseInterceptor(failures: int.MaxValue, transient: false);
        await using var db = CreateContext(fixture.NewDatabaseConnectionString(), broken);

        await Should.ThrowAsync<NpgsqlException>(
            RunAdvancingTimeAsync(Migrator.MigrateAsync(db, TestContext.Current.CancellationToken)));

        broken.Attempts.ShouldBe(1);
    }

    private Task<T> RunAdvancingTimeAsync<T>(Task<T> task) => _time.AdvanceUntilCompletedAsync(task, DatabaseMigrator.RetryDelay);

    private static ApolloDbContext CreateContext(string connectionString) => new(ApolloDbContext.CreateOptions(connectionString));

    private static ApolloDbContext CreateContext(string connectionString, UnavailableDatabaseInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<ApolloDbContext>(ApolloDbContext.CreateOptions(connectionString))
            .AddInterceptors(interceptor)
            .Options);

    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
