using Apollo.Data;
using Apollo.Testing;
using Apollo.Testing.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.IntegrationTests.Data;

[Collection<TimescaleCollection>]
public sealed class MigrationExtensionsTests(TimescaleFixture fixture)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task WaitForMigrationsAsync_WhenNoMigrationsPending_ShouldReturnWithoutWaiting()
    {
        using var host = BuildHost(fixture.ConnectionString);

        var wait = host.Services.WaitForMigrationsAsync(TestContext.Current.CancellationToken);

        await wait.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        _time.GetUtcNow().ShouldBe(_time.Start);
    }

    [Fact]
    public async Task WaitForMigrationsAsync_WhenMigrationsPending_ShouldWaitUntilApplied()
    {
        var connectionString = fixture.NewDatabaseConnectionString();
        using var host = BuildHost(connectionString);

        var wait = host.Services.WaitForMigrationsAsync(TestContext.Current.CancellationToken);
        for (var poll = 0; poll < 3; poll++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
            _time.Advance(MigrationExtensions.PollInterval);
        }

        wait.IsCompleted.ShouldBeFalse();

        await MigrateAsync(connectionString);
        await _time.AdvanceUntilCompletedAsync(wait, MigrationExtensions.PollInterval);
    }

    [Fact]
    public async Task WaitForMigrationsAsync_WhenTimeoutExceeded_ShouldThrow()
    {
        using var host = BuildHost(fixture.NewDatabaseConnectionString());

        var wait = host.Services.WaitForMigrationsAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<TimeoutException>(_time.AdvanceUntilCompletedAsync(wait, MigrationExtensions.PollInterval));
        _time.GetUtcNow().ShouldBeGreaterThanOrEqualTo(_time.Start + _timeout);
    }

    private IHost BuildHost(string connectionString)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ApolloDbContext.ConnectionName}"] = connectionString,
            [$"{DataOptions.SectionName}:{nameof(DataOptions.MigrationWaitTimeout)}"] = _timeout.ToString(),
        });
        builder.AddApolloDbContext();
        builder.Services.AddSingleton<TimeProvider>(_time);

        return builder.Build();
    }

    private static async Task MigrateAsync(string connectionString)
    {
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions(connectionString));
        await new DatabaseMigrator(TimeProvider.System, NullLogger<DatabaseMigrator>.Instance)
            .MigrateAsync(db, TestContext.Current.CancellationToken);
    }
}
