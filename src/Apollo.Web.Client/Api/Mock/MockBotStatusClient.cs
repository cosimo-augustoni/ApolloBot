using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IBotStatusClient"/> without a host, for the prototype and bUnit tests: a connected bot with a heartbeat
/// 7 s ago and a day of sample events. Tests set <see cref="Status"/> (or <see cref="Failure"/>) before or between
/// loads, and read <see cref="Calls"/>.
/// </summary>
public sealed class MockBotStatusClient : IBotStatusClient
{
    public MockBotStatusClient(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        var nowUtc = now.UtcDateTime;
        var currentHour = new DateTimeOffset(nowUtc.Date.AddHours(nowUtc.Hour), TimeSpan.Zero);
        Status = new BotStatusDto(
            now,
            now.AddSeconds(-7),
            Connected: true,
            GatewayLatencyMs: 84,
            now.AddSeconds(-12),
            OpenVoiceSessions: 3,
            IsStale: false,
            [.. Enumerable.Range(0, 24).Select(i => new HourlyEventCountDto(currentHour.AddHours(i - 23), SampleCount(i)))]);
    }

    /// <summary>What the next load returns.</summary>
    public BotStatusDto Status { get; set; }

    /// <summary>When set, the next load throws it instead.</summary>
    public Exception? Failure { get; set; }

    /// <summary>How often the status was loaded.</summary>
    public int Calls { get; private set; }

    public Task<BotStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Failure is { } failure ? Task.FromException<BotStatusDto>(failure) : Task.FromResult(Status);
    }

    // Deterministic, quiet at night and busy in the evening.
    private static int SampleCount(int hour) => (int)Math.Round(40 + (35 * Math.Sin((hour - 6) / 24.0 * 2 * Math.PI)));
}
