using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Membership;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Membership;

/// <summary>
/// Records joins, leaves, bans and unbans of tracked users into <c>membership_events</c> (#18, scope §5.1). It runs
/// next to <c>GuildStateSync</c>, which handles the same join and leave events for <c>guild_members</c>.
/// </summary>
/// <remarks>
/// <para>
/// Join and unban count if the tracking gate tracks the user when the event is processed (a join only after the sync
/// stored the new member, so the recorder is registered after it). Leave and ban count if the user had a tracking
/// period at <c>occurred_at</c>: the leave itself stops tracking, whichever handler runs first. A ban also counts if
/// the period ended at most <see cref="BanGrace"/> before it, because Discord sends the member remove of a ban as a
/// separate event that may arrive (and close the period) first.
/// </para>
/// <para>
/// Replays: the payloads carry no timestamp, so a replayed event has a new <c>occurred_at</c>. An event is therefore
/// a duplicate if the user already has a row of the same kind and the tracking did not change since the latest one:
/// for a leave or ban, no tracking period started after that row (a real second leave needs a rejoin while tracked); for a
/// join or unban, no tracking period ended after it (a real second join needs a leave first).
/// </para>
/// </remarks>
internal sealed partial class MembershipRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    TrackingHistory history,
    RecordedEventMetrics metrics,
    ILogger<MembershipRecorder> logger)
    : IPlatformEventHandler<PlatformMemberJoined>,
        IPlatformEventHandler<PlatformMemberLeft>,
        IPlatformEventHandler<PlatformMemberBanned>,
        IPlatformEventHandler<PlatformMemberUnbanned>
{
    /// <summary>
    /// How long before a ban the user's tracking period may have ended for the ban to count (see remarks).
    /// </summary>
    public static readonly TimeSpan BanGrace = TimeSpan.FromSeconds(10);

    public Task HandleAsync(PlatformMemberJoined platformEvent, CancellationToken cancellationToken) =>
        RecordIfTrackedNowAsync(platformEvent, platformEvent.Member.User, MembershipEventKind.Join, cancellationToken);

    public Task HandleAsync(PlatformMemberUnbanned platformEvent, CancellationToken cancellationToken) =>
        RecordIfTrackedNowAsync(platformEvent, platformEvent.User, MembershipEventKind.Unban, cancellationToken);

    public Task HandleAsync(PlatformMemberLeft platformEvent, CancellationToken cancellationToken) =>
        RecordIfTrackedAtAsync(platformEvent, platformEvent.User, MembershipEventKind.Leave, TimeSpan.Zero, cancellationToken);

    public Task HandleAsync(PlatformMemberBanned platformEvent, CancellationToken cancellationToken) =>
        RecordIfTrackedAtAsync(platformEvent, platformEvent.User, MembershipEventKind.Ban, BanGrace, cancellationToken);

    private async Task RecordIfTrackedNowAsync(
        PlatformEvent platformEvent,
        PlatformRef user,
        MembershipEventKind kind,
        CancellationToken cancellationToken)
    {
        if (gate.TryGetTrackedUserId(user) is not { } userId)
        {
            Drop(platformEvent, user);
            return;
        }

        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        await RecordAsync(platformEvent, guildId, userId, kind, cancellationToken);
    }

    private async Task RecordIfTrackedAtAsync(
        PlatformEvent platformEvent,
        PlatformRef user,
        MembershipEventKind kind,
        TimeSpan grace,
        CancellationToken cancellationToken)
    {
        // Never creates a row: a user or guild the bot has not seen was never tracked.
        if (await ids.TryGetUserAsync(user, cancellationToken) is not { } userId
            || await ids.TryGetGuildAsync(platformEvent.Guild, cancellationToken) is not { } guildId
            || !await history.WasTrackedBetweenAsync(guildId, userId, platformEvent.OccurredAt - grace, platformEvent.OccurredAt, cancellationToken))
        {
            Drop(platformEvent, user);
            return;
        }

        await RecordAsync(platformEvent, guildId, userId, kind, cancellationToken);
    }

    private async Task RecordAsync(
        PlatformEvent platformEvent,
        Guid guildId,
        Guid userId,
        MembershipEventKind kind,
        CancellationToken cancellationToken)
    {
        if (await IsReplayAsync(guildId, userId, kind, cancellationToken))
        {
            LogReplay(kind, userId);
            return;
        }

        db.MembershipEvents.Add(new MembershipEvent
        {
            GuildId = guildId,
            OccurredAt = platformEvent.OccurredAt,
            UserId = userId,
            Kind = kind,
        });
        await db.SaveChangesAsync(cancellationToken);
        metrics.Recorded(platformEvent);
        LogRecorded(kind, userId);
    }

    /// <summary>
    /// Whether the user's latest row of <paramref name="kind"/> already stands for this event (see remarks).
    /// </summary>
    private async Task<bool> IsReplayAsync(Guid guildId, Guid userId, MembershipEventKind kind, CancellationToken cancellationToken)
    {
        var latest = await db.MembershipEvents
            .Where(e => e.GuildId == guildId && e.UserId == userId && e.Kind == kind)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => (DateTimeOffset?)e.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is not { } since)
        {
            return false;
        }

        var periods = db.TrackingPeriods.Where(p => p.GuildId == guildId && p.UserId == userId);
        var trackingChangedSince = kind is MembershipEventKind.Leave or MembershipEventKind.Ban
            ? await periods.AnyAsync(p => p.From > since, cancellationToken)
            : await periods.AnyAsync(p => p.To > since, cancellationToken);
        return !trackingChangedSince;
    }

    private void Drop(PlatformEvent platformEvent, PlatformRef user)
    {
        metrics.Dropped(EventDropReason.Untracked);
        LogDropped(platformEvent.GetType().Name, user);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded membership {Kind} for {UserId}")]
    private partial void LogRecorded(MembershipEventKind kind, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipped membership {Kind} for {UserId}: replay of the latest row")]
    private partial void LogReplay(MembershipEventKind kind, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {EventType} for {User}: user untracked")]
    private partial void LogDropped(string eventType, PlatformRef user);
}
