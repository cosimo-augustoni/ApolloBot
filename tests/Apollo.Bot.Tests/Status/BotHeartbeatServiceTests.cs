using Apollo.Bot.Status;
using Apollo.Data;
using Apollo.Data.Status;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Bot.Tests.Status;

public sealed class BotHeartbeatServiceTests : TimescaleTest
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(10);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly FakePlatformConnectionStatus _connection;
    private IHost? _host;

    public BotHeartbeatServiceTests(TimescaleFixture fixture)
        : base(fixture) => _connection = new FakePlatformConnectionStatus(_time);

    [Fact]
    public async Task Heartbeat_When30SecondsPass_ShouldWriteExactlyOneRowAtTheCurrentTime()
    {
        await StartAsync();

        _time.Advance(_interval);
        await WaitForRowsAsync(1);
        // Stopping waits for any write still in flight, so the count below is final, not just "at least one".
        await _host!.StopAsync(TestContext.Current.CancellationToken);

        var row = (await ReadRowsAsync()).ShouldHaveSingleItem();
        row.OccurredAt.ShouldBe(TestTime.Now + _interval);
    }

    [Fact]
    public async Task Heartbeat_WhenLessThan30SecondsPass_ShouldWriteNoRow()
    {
        await StartAsync();

        _time.Advance(_interval - TimeSpan.FromMilliseconds(1));
        await _host!.StopAsync(TestContext.Current.CancellationToken);

        (await ReadRowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Heartbeat_WhenThreeIntervalsPass_ShouldWriteExactlyThreeRowsThirtySecondsApart()
    {
        await StartAsync();

        for (var tick = 1; tick <= 3; tick++)
        {
            _time.Advance(_interval);
            await WaitForRowsAsync(tick);
        }

        await _host!.StopAsync(TestContext.Current.CancellationToken);

        (await ReadRowsAsync()).Select(r => r.OccurredAt)
            .ShouldBe([TestTime.Now + _interval, TestTime.Now + (2 * _interval), TestTime.Now + (3 * _interval)]);
    }

    [Fact]
    public async Task Heartbeat_WhenGatewayIsConnectedWithKnownLatency_ShouldWriteConnectedAndThatLatency()
    {
        _connection.Connect(TimeSpan.FromMilliseconds(87));
        await StartAsync();

        _time.Advance(_interval);

        var row = (await WaitForRowsAsync(1)).ShouldHaveSingleItem();
        row.Connected.ShouldBeTrue();
        row.GatewayLatencyMs.ShouldBe(87);
    }

    [Fact]
    public async Task Heartbeat_WhenGatewayIsDisconnected_ShouldStillWriteARowWithConnectedFalse()
    {
        _connection.Connect(TimeSpan.FromMilliseconds(87)).Disconnect();
        await StartAsync();

        _time.Advance(_interval);

        var row = (await WaitForRowsAsync(1)).ShouldHaveSingleItem();
        row.Connected.ShouldBeFalse();
        row.GatewayLatencyMs.ShouldBeNull();
    }

    [Fact]
    public async Task Heartbeat_WhenWritten_ShouldReferenceTheInternalGuildAndCarryLastEventAndVoiceSessions()
    {
        await StartAsync(services => services.AddSingleton<IOpenVoiceSessionCounter>(new FixedOpenVoiceSessionCounter(3)));
        var platform = new FakeChatPlatform(_host!.Services.GetRequiredService<IPlatformEventDispatcher>(), _time);
        await platform.RaiseAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);
        var lastEventAt = _time.GetUtcNow();

        _time.Advance(_interval);

        var row = (await WaitForRowsAsync(1)).ShouldHaveSingleItem();
        row.LastEventAt.ShouldBe(lastEventAt);
        row.OpenVoiceSessions.ShouldBe(3);
        await using var db = Fixture.CreateDbContext();
        var guildId = await db.GuildPlatformIds.Where(g => g.ExternalId == TestPlatform.Guild.ExternalId)
            .Select(g => g.GuildId).SingleAsync(TestContext.Current.CancellationToken);
        row.GuildId.ShouldBe(guildId);
    }

    [Fact]
    public async Task Heartbeat_WhenTheDatabaseFailsOnce_ShouldLogTheErrorAndWriteOnTheNextTick()
    {
        var failing = new FailingSaveChangesInterceptor(failures: 1);
        await StartAsync(services => services.ConfigureDbContext<ApolloDbContext>(o => o.AddInterceptors(failing)));
        var logs = _host!.Services.GetFakeLogCollector();

        _time.Advance(_interval);
        await WaitUntilAsync(() => Task.FromResult(logs.GetSnapshot().Any(r => r.Level == LogLevel.Error)));
        _time.Advance(_interval);

        var row = (await WaitForRowsAsync(1)).ShouldHaveSingleItem();
        row.OccurredAt.ShouldBe(TestTime.Now + (2 * _interval));
        var error = logs.GetSnapshot().Single(r => r.Level == LogLevel.Error);
        error.Exception.ShouldBeOfType<InvalidOperationException>();
        error.Category.ShouldBe(typeof(BotHeartbeatService).FullName);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }

        await base.DisposeAsync();
    }

    private async Task StartAsync(Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{ApolloDbContext.ConnectionName}"] = Fixture.ConnectionString,
        });
        builder.Services.AddFakeLogging();
        builder.Services.AddSingleton<TimeProvider>(_time);
        builder.Services.AddSingleton<IPlatformConnectionStatus>(_connection);
        builder.AddApolloDbContext();
        configure?.Invoke(builder.Services);
        builder.Services.TryAddSingleton<IOpenVoiceSessionCounter>(new FixedOpenVoiceSessionCounter(0));
        builder.Services.AddBotStatus();
        _host = builder.Build();
        await _host.StartAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<BotStatus>> WaitForRowsAsync(int count)
    {
        List<BotStatus> rows = [];
        await WaitUntilAsync(async () =>
        {
            rows = await ReadRowsAsync();
            return rows.Count >= count;
        });
        return rows;
    }

    private async Task<List<BotStatus>> ReadRowsAsync()
    {
        await using var db = Fixture.CreateDbContext();
        return await db.BotStatuses.AsNoTracking().OrderBy(r => r.OccurredAt).ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The heartbeat writes on its own task after the fake clock fires, so tests poll (on the wall clock) for the result.
    /// </summary>
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!await condition())
        {
            waited.Elapsed.ShouldBeLessThan(_wait, "the heartbeat did not get there in time");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }
    }
}
