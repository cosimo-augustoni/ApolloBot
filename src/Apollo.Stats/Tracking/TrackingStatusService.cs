using Apollo.Data;
using Apollo.Stats.Activity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Stats.Tracking;

/// <summary>
/// Reads a member's <c>tracking_periods</c> (scope §5.1.1): the open period (at most one per member) gives
/// <see cref="TrackingStatus.TrackedSince"/>, the latest closed one <see cref="TrackingStatus.TrackedUntil"/>.
/// </summary>
public sealed class TrackingStatusService(ApolloDbContext db) : ITrackingStatusService
{
    public async Task<TrackingStatus> GetAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        var periods = await db.TrackingPeriods.AsNoTracking()
            .Where(p => p.GuildId == guildId && p.UserId == userId)
            .Select(p => new TrackingSpan(p.From, p.To))
            .ToListAsync(cancellationToken);

        var since = periods.Where(p => p.To is null).Select(p => (DateTimeOffset?)p.From).SingleOrDefault();
        var until = periods.Max(p => p.To);
        return new TrackingStatus(
            since is { } from ? ZurichCalendar.DayOf(from) : null,
            until is { } to ? ZurichCalendar.DayOf(to) : null);
    }
}
