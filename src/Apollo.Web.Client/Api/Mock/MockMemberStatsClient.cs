using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IMemberStatsClient"/> without a host, for the prototype and bUnit tests: any user ID is "Anna", tracked
/// since 03.02.2025, with a share of <see cref="MockServerStatsClient.Sample"/> and a full set of highlights, games
/// included. Tests set <see cref="Answer"/> (or <see cref="Failure"/>, or <see cref="Gate"/> to hold a load open) and
/// read <see cref="Requests"/>.
/// </summary>
public sealed class MockMemberStatsClient : IMemberStatsClient
{
    /// <summary>Anna's tracking start in the sample.</summary>
    public static readonly DateOnly SampleTrackedSince = new(2025, 2, 3);

    private const double MemberShare = 0.06;

    /// <summary>What a load returns for its user and range; <see cref="Sample"/> by default, <see langword="null"/> for an unknown member.</summary>
    public Func<Guid, DateOnly, DateOnly, MemberStatsDto?> Answer { get; set; } = Sample;

    /// <summary>When set, loads throw it instead.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When set, loads wait for it before they answer.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>The users and ranges loaded, in order.</summary>
    public List<(Guid UserId, DateOnly From, DateOnly To)> Requests { get; } = [];

    public async Task<MemberStatsDto?> GetAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        Requests.Add((userId, from, to));
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return Failure is { } failure ? throw failure : Answer(userId, from, to);
    }

    /// <summary>Anna, tracked, busy evenings: a share of the server sample, with emojis, voice buddies, replies and games.</summary>
    public static MemberStatsDto Sample(Guid userId, DateOnly from, DateOnly to)
    {
        var server = MockServerStatsClient.Sample(from, to);
        var daily = server.Daily.Select(d => new DailyActivityDto(d.Date, Share(d.Counts))).ToArray();
        var totals = Share(server.Totals);
        var days = (double)daily.Length;
        var activity = server with
        {
            Totals = totals,
            Averages = new ActivityAveragesDto(
                totals.Messages / days,
                totals.Characters / days,
                totals.ReactionsGiven / days,
                totals.ReactionsReceived / days,
                totals.VoiceSeconds / days),
            ActiveDays = daily.Count(d => d.Counts.Messages > 0 || d.Counts.ReactionsGiven > 0 || d.Counts.VoiceSeconds > 0),
            ActiveUsers = null,
            TrackedDays = daily.Length,
            TrackedSince = SampleTrackedSince,
            Daily = daily,
            Channels = [.. server.Channels.Select(c => c with { Messages = Share(c.Messages), VoiceSeconds = Share(c.VoiceSeconds) })],
            Heatmap = [.. server.Heatmap.Select(day => (IReadOnlyList<long>)[.. day.Select(Share)])],
            VoiceHeatmap = [.. server.VoiceHeatmap.Select(day => (IReadOnlyList<long>)[.. day.Select(Share)])],
        };

        var hours = Math.Max(1, totals.VoiceSeconds / 3600);
        return new MemberStatsDto(
            new MemberProfileDto(userId, "Anna", AvatarUrl: null, "Discord"),
            new TrackingStatusDto(SampleTrackedSince, TrackedUntil: null),
            activity,
            new MemberHighlightsDto(
                [new("😂", false, 41), new("👍", false, 27), new("❤️", false, 15), new("apollo", true, 6)],
                [new(Ben, hours * 1900), new(Chris, hours * 1200), new(Dana, hours * 450)],
                [new(Ben, 34), new(Dana, 12)],
                [new(Chris, 29), new(Ben, 21)],
                new LongestVoiceSessionDto(new EventChannelDto(new Guid("6a1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e04"), "Voice-Lounge"),
                    new DateTimeOffset(to.ToDateTime(new TimeOnly(19, 12)), TimeSpan.Zero), (4 * 3600) + (37 * 60)),
                [new("Factorio", 8 * 3600), new("Rocket League", (2 * 3600) + (24 * 60))]));
    }

    /// <summary>A member who was never tracked: no tracking period, so a zero-filled range and no highlights.</summary>
    public static MemberStatsDto NeverTracked(Guid userId, DateOnly from, DateOnly to)
    {
        var empty = MockServerStatsClient.Empty(from, to);
        return new MemberStatsDto(
            new MemberProfileDto(userId, "Anna", AvatarUrl: null, "Discord"),
            new TrackingStatusDto(TrackedSince: null, TrackedUntil: null),
            empty with { ActiveUsers = null, TrackedDays = 0 },
            MemberHighlightsDto.Empty);
    }

    private static EventUserDto Ben => new(new Guid("0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d01"), "Ben");

    private static EventUserDto Chris => new(new Guid("0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d02"), "Chris");

    private static EventUserDto Dana => new(new Guid("0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d03"), null);

    private static ActivityCountsDto Share(ActivityCountsDto counts) => new(
        Share(counts.Messages),
        Share(counts.Characters),
        Share(counts.ReactionsGiven),
        Share(counts.ReactionsReceived),
        Share(counts.VoiceSeconds));

    private static long Share(long value) => (long)Math.Round(value * MemberShare);
}
