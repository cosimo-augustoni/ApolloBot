using Apollo.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Tracking;

/// <summary>
/// Whether a user was tracked at a past moment, from <c>tracking_periods</c> (#18), and since when they are tracked
/// (#21). Unlike <see cref="ITrackingGate"/>, which answers for now, this answers for an event's <c>occurred_at</c>
/// after the event itself may have stopped the tracking (a leave closes the period).
/// </summary>
internal sealed class TrackingHistory(ApolloDbContext db)
{
    /// <summary>
    /// Whether the user had a tracking period that covers <paramref name="at"/>; both ends are inclusive.
    /// </summary>
    public Task<bool> WasTrackedAtAsync(Guid guildId, Guid userId, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.TrackingPeriods.AnyAsync(
            p => p.GuildId == guildId && p.UserId == userId && p.From <= at && (p.To == null || p.To >= at),
            cancellationToken);

    /// <summary>
    /// When the user's open tracking period started; <see langword="null"/> without one.
    /// </summary>
    public Task<DateTimeOffset?> OpenPeriodStartAsync(Guid guildId, Guid userId, CancellationToken cancellationToken) =>
        db.TrackingPeriods
            .Where(p => p.GuildId == guildId && p.UserId == userId && p.To == null)
            .Select(p => (DateTimeOffset?)p.From)
            .FirstOrDefaultAsync(cancellationToken);
}
