using Apollo.Stats.Activity;
using Contracts = Apollo.Web.Client.Contracts;

namespace Apollo.Web.Admin;

/// <summary>
/// Maps the stats layer's <see cref="ActivityStatsDto"/> (#35) to the API contract the client shares
/// (<see cref="Contracts.ActivityStatsDto"/>): the client cannot reference <c>Apollo.Stats</c>, and an explicit copy keeps
/// a change of the stats layer from changing the API unnoticed.
/// </summary>
public static class ActivityStatsContract
{
    public static Contracts.ActivityStatsDto ToContract(this ActivityStatsDto stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return new Contracts.ActivityStatsDto(
            stats.From,
            stats.To,
            stats.Totals.ToContract(),
            new Contracts.ActivityAveragesDto(
                stats.Averages.Messages,
                stats.Averages.Characters,
                stats.Averages.ReactionsGiven,
                stats.Averages.ReactionsReceived,
                stats.Averages.VoiceSeconds),
            stats.ActiveDays,
            stats.ActiveUsers,
            stats.TrackedDays,
            stats.TrackedSince,
            [.. stats.Daily.Select(d => new Contracts.DailyActivityDto(d.Date, d.Counts.ToContract()))],
            [.. stats.Channels.Select(c => new Contracts.ChannelActivityDto(c.ChannelId, c.Name, c.IsDeleted, c.Messages, c.VoiceSeconds))],
            stats.Heatmap);
    }

    private static Contracts.ActivityCountsDto ToContract(this ActivityCounts counts) =>
        new(counts.Messages, counts.Characters, counts.ReactionsGiven, counts.ReactionsReceived, counts.VoiceSeconds);
}
