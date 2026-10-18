using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Formatting;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IDataSanityClient"/> without a host, for the prototype and bUnit tests: two heartbeat gaps, a long and an
/// open voice session, and no data of untracked members. Without a range it answers the last 30 Zurich days up to
/// today, as the host does. Tests set <see cref="Answer"/> (or <see cref="Failure"/>, or <see cref="Gate"/> to hold a
/// load open) and read <see cref="Requests"/>.
/// </summary>
public sealed class MockDataSanityClient(TimeProvider time) : IDataSanityClient
{
    private static readonly EventUserDto _mara = new(new Guid("5b2e4c1d-3f6a-4b7c-8d9e-0f1a2b3c4d01"), "Mara");
    private static readonly EventUserDto _jonas = new(new Guid("5b2e4c1d-3f6a-4b7c-8d9e-0f1a2b3c4d02"), "Jonas");

    /// <summary>What a load returns for its range; <see cref="Sample"/> by default.</summary>
    public Func<DateOnly, DateOnly, DataSanityDto> Answer { get; set; } = Sample;

    /// <summary>When set, loads throw it instead.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, loads wait for it before they answer.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>The ranges loaded, in order; <see langword="null"/> for the host's default.</summary>
    public List<(DateOnly? From, DateOnly? To)> Requests { get; } = [];

    public async Task<DataSanityDto> GetAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        Requests.Add((from, to));
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        var today = UiTime.DayOf(time.GetUtcNow());
        return Failure is { } failure ? throw failure : Answer(from ?? today.AddDays(-29), to ?? today);
    }

    /// <summary>
    /// Two heartbeat gaps (the newer one 1 h 4 min long, four days before the end), a 26-hour voice session, a voice
    /// session open for three days, and nothing untracked.
    /// </summary>
    public static DataSanityDto Sample(DateOnly from, DateOnly to)
    {
        var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var gap = end.AddDays(-4).AddHours(3).AddMinutes(12).AddSeconds(30);
        return new DataSanityDto(
            from,
            to,
            HeartbeatGapThresholdSeconds: 120,
            new SanityCheckDto<HeartbeatGapDto>(
                2,
                [
                    new HeartbeatGapDto(gap, gap.AddMinutes(64).AddSeconds(15)),
                    new HeartbeatGapDto(end.AddDays(-9).AddHours(21), end.AddDays(-9).AddHours(21).AddMinutes(3)),
                ]),
            new SanityCheckDto<SanitySessionDto>(
                1,
                [new SanitySessionDto("voice_sessions", 1812, _mara, end.AddDays(-6).AddHours(19), end.AddDays(-5).AddHours(21))]),
            new SanityCheckDto<SanitySessionDto>(
                1,
                [new SanitySessionDto("voice_sessions", 2045, _jonas, end.AddDays(-3).AddHours(20), null)]),
            new SanityCheckDto<UntrackedRowDto>(0, []),
            new SanityCheckDto<UntrackedRowDto>(0, []));
    }
}
