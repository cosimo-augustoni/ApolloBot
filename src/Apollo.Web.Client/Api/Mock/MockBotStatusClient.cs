using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IBotStatusClient"/> without a host, for the prototype and bUnit tests: a connected bot with a heartbeat
/// 7 s ago, a tracking role set, three members in two voice channels and a day of sample events. Tests set
/// <see cref="Status"/> (or <see cref="Failure"/>) before or between loads, and read <see cref="Calls"/>.
/// </summary>
public sealed class MockBotStatusClient : IBotStatusClient
{
    // The tables the bot status counts per hour, with their share of the sample events.
    private static readonly (DataEventType Type, double Share)[] _sampleShares =
    [
        (DataEventType.Message, 0.6),
        (DataEventType.VoiceSession, 0.1),
        (DataEventType.Reaction, 0.2),
        (DataEventType.Membership, 0.02),
        (DataEventType.PresenceActivity, 0.08),
    ];

    private static readonly Guid _lounge = new("0192d000-0000-7000-8000-000000000001");
    private static readonly Guid _gaming = new("0192d000-0000-7000-8000-000000000002");

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
            LastConnectedAt: now.AddSeconds(-7),
            GatewayLatencyMeanMs: 84,
            new LastRecordedEventDto(DataEventType.Message, now.AddSeconds(-12), "allgemein"),
            [
                new OpenVoiceSessionDto("Anna", _lounge, "Voice-Lounge", now.AddMinutes(-50)),
                new OpenVoiceSessionDto("Ben", _lounge, "Voice-Lounge", now.AddMinutes(-18)),
                new OpenVoiceSessionDto("Chris", _gaming, "Gaming", now.AddMinutes(-15)),
            ],
            IsStale: false,
            NoTrackingRole: false,
            [.. Enumerable.Range(0, 24).Select(i => SampleHour(currentHour.AddHours(i - 23), i))]);
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

    private static HourlyEventCountDto SampleHour(DateTimeOffset hourStart, int hour)
    {
        List<EventTypeCountDto> types = [.. _sampleShares.Select(s => new EventTypeCountDto(s.Type, (int)Math.Round(SampleCount(hour) * s.Share)))];
        return new HourlyEventCountDto(hourStart, types.Sum(t => t.Count), types);
    }

    // Deterministic, quiet at night and busy in the evening.
    private static int SampleCount(int hour) => (int)Math.Round(40 + (35 * Math.Sin((hour - 6) / 24.0 * 2 * Math.PI)));
}
