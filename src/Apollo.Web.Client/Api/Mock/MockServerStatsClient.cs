using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IServerStatsClient"/> without a host, for the prototype and bUnit tests: deterministic sample activity for
/// any range (busy evenings and weekends, five channels, one of them deleted). Tests set <see cref="Answer"/> (or
/// <see cref="Failure"/>, or <see cref="Gate"/> to hold a load open) and read <see cref="Requests"/>.
/// </summary>
public sealed class MockServerStatsClient : IServerStatsClient
{
    private static readonly (Guid Id, string Name, bool IsDeleted, double MessageShare, double VoiceShare)[] _channels =
    [
        (new Guid("6a1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e01"), "allgemein", false, 0.45, 0),
        (new Guid("6a1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e02"), "memes", false, 0.25, 0),
        (new Guid("6a1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e03"), "gaming", false, 0.18, 0),
        (new Guid("6a1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e04"), "Voice-Lounge", false, 0.02, 0.7),
        (new Guid("6a1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e05"), "alte-ankündigungen", true, 0.10, 0.3),
    ];

    /// <summary>What a load returns for its range; <see cref="Sample"/> by default.</summary>
    public Func<DateOnly, DateOnly, ActivityStatsDto> Answer { get; set; } = Sample;

    /// <summary>When set, loads throw it instead.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, loads wait for it before they answer.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>The ranges loaded, in order.</summary>
    public List<(DateOnly From, DateOnly To)> Requests { get; } = [];

    public async Task<ActivityStatsDto> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Requests.Add((from, to));
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return Failure is { } failure ? throw failure : Answer(from, to);
    }

    /// <summary>Sample activity: every day busy, weekends more, with the channel ranking and heatmap to match.</summary>
    public static ActivityStatsDto Sample(DateOnly from, DateOnly to) =>
        Build(from, to, day =>
        {
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 1.4 : 1.0;
            var wave = 0.7 + (0.3 * Math.Sin(day.DayNumber / 3.0));
            var messages = (long)Math.Round(420 * weekend * wave);
            var reactions = messages / 4;
            return new ActivityCountsDto(messages, messages * 38, reactions, reactions, (long)Math.Round(5.5 * 3600 * weekend * wave));
        });

    /// <summary>A range without any activity: zero-filled, as the host answers it.</summary>
    public static ActivityStatsDto Empty(DateOnly from, DateOnly to) =>
        Build(from, to, _ => new ActivityCountsDto(0, 0, 0, 0, 0));

    private static ActivityStatsDto Build(DateOnly from, DateOnly to, Func<DateOnly, ActivityCountsDto> countsOf)
    {
        var daily = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(i => from.AddDays(i))
            .Select(day => new DailyActivityDto(day, countsOf(day)))
            .ToArray();
        var totals = new ActivityCountsDto(
            daily.Sum(d => d.Counts.Messages),
            daily.Sum(d => d.Counts.Characters),
            daily.Sum(d => d.Counts.ReactionsGiven),
            daily.Sum(d => d.Counts.ReactionsReceived),
            daily.Sum(d => d.Counts.VoiceSeconds));
        var days = (double)daily.Length;
        var activeDays = daily.Count(d => d.Counts.Messages > 0 || d.Counts.ReactionsGiven > 0 || d.Counts.VoiceSeconds > 0);

        return new ActivityStatsDto(
            from,
            to,
            totals,
            new ActivityAveragesDto(
                totals.Messages / days,
                totals.Characters / days,
                totals.ReactionsGiven / days,
                totals.ReactionsReceived / days,
                totals.VoiceSeconds / days),
            activeDays,
            ActiveUsers: activeDays == 0 ? 0 : 42,
            TrackedDays: null,
            TrackedSince: null,
            daily,
            [
                .. _channels
                    .Select(c => new ChannelActivityDto(c.Id, c.Name, c.IsDeleted, (long)(totals.Messages * c.MessageShare), (long)(totals.VoiceSeconds * c.VoiceShare)))
                    .Where(c => c.Messages > 0 || c.VoiceSeconds > 0)
                    .OrderByDescending(c => c.Messages)
                    .ThenByDescending(c => c.VoiceSeconds),
            ],
            Heatmap(totals.Messages),
            Heatmap(totals.VoiceSeconds));
    }

    // Evenings and weekends busiest; the cells add up to roughly the total (messages, or voice seconds).
    private static IReadOnlyList<IReadOnlyList<long>> Heatmap(long total)
    {
        var weights = Enumerable.Range(0, 7)
            .Select(day => Enumerable.Range(0, 24).Select(hour => Weight(day, hour)).ToArray())
            .ToArray();
        var sum = weights.Sum(day => day.Sum());
        return [.. weights.Select(day => (IReadOnlyList<long>)[.. day.Select(w => (long)Math.Round(total * w / sum))])];
    }

    private static double Weight(int day, int hour)
    {
        var evening = 0.7 * Math.Exp(-Math.Pow(hour - 20.5, 2) / 10.0);
        var midday = 0.3 * Math.Exp(-Math.Pow(hour - 12.5, 2) / 6.0);
        var night = hour < 7 ? 0.0 : 0.08;
        var weekend = day >= 5 ? 1.35 : 1.0;
        return (evening + midday + night) * weekend;
    }
}
