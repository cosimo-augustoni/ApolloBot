using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Status;

namespace Apollo.Bot.Status;

/// <summary>
/// Appends one <c>bot_status</c> row every <see cref="Interval"/> (#24), connected or not, so the admin status page
/// (#31) and the sanity report (#40) can tell "no activity" from "bot down". A failed write is logged and the next
/// tick tries again.
/// </summary>
internal sealed partial class BotHeartbeatService(
    IServiceScopeFactory scopeFactory,
    IBotStatusSource statusSource,
    TimeProvider timeProvider,
    ILogger<BotHeartbeatService> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private PeriodicTimer? _timer;

    /// <summary>
    /// Creates the timer before <see cref="ExecuteAsync"/> starts (on a thread-pool thread since .NET 10), so the first
    /// tick is due exactly one <see cref="Interval"/> after the host started.
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(Interval, timeProvider);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _timer ?? throw new InvalidOperationException("The heartbeat was not started.");
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await WriteHeartbeatAsync(stoppingToken);
        }
    }

    private async Task WriteHeartbeatAsync(CancellationToken cancellationToken)
    {
        var occurredAt = timeProvider.GetUtcNow();
        try
        {
            var status = statusSource.GetCurrent();

            await using var scope = scopeFactory.CreateAsyncScope();
            var resolver = scope.ServiceProvider.GetRequiredService<IPlatformIdResolver>();
            var db = scope.ServiceProvider.GetRequiredService<ApolloDbContext>();

            db.BotStatuses.Add(new BotStatus
            {
                GuildId = await resolver.ResolveGuildAsync(status.Guild, cancellationToken),
                OccurredAt = occurredAt,
                Connected = status.Connected,
                GatewayLatencyMs = status.GatewayLatency is { } latency ? (int)Math.Round(latency.TotalMilliseconds) : null,
                LastEventAt = status.LastEventAt,
                OpenVoiceSessions = status.OpenVoiceSessions,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogHeartbeatFailed(exception, occurredAt);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write the bot heartbeat for {OccurredAt}; retrying on the next tick")]
    private partial void LogHeartbeatFailed(Exception exception, DateTimeOffset occurredAt);
}
