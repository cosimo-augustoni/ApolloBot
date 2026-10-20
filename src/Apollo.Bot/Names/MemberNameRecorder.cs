using Apollo.Bot.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Names;
using Apollo.Domain.Names;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Names;

/// <summary>
/// Records the name history of tracked members (#114, scope §5.1): their server nickname, global display name and
/// username, kept apart, as <c>member_name_changes</c>. <see cref="MemberNameHistory"/> decides what a row is.
/// </summary>
/// <remarks>
/// <para>
/// Inputs: member updates (stamped on receipt, they carry no timestamp); the tracking start, which stores the baseline
/// with the member's current names (also for everyone tracked after the first sync on startup, so members tracked
/// before #114 get theirs then); and every full guild sync (startup, reconnect), which compares every tracked member's
/// current names with their latest row, so a change while the bot was down or disconnected is stored at the sync. The
/// sync and the tracking start read the names from the platform's cache under <see cref="MemberNameLock"/>, so they
/// include every update received before; an update that waited for the lock meanwhile is older than their row and
/// stores nothing.
/// </para>
/// <para>
/// It compares with its own table, never with <c>users.display_name</c>, so its order relative to
/// <see cref="GuildStateSync"/> for the same update does not matter: a member who gains tracking through it gets one
/// baseline either way. Untracked members are never stored, and a row is written only while the member has an open
/// tracking period.
/// </para>
/// </remarks>
internal sealed partial class MemberNameRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    IChatPlatform platform,
    MemberNameLock nameLock,
    RecordedEventMetrics metrics,
    TimeProvider timeProvider,
    ILogger<MemberNameRecorder> logger)
    : IPlatformEventHandler<PlatformMemberUpdated>,
        ITrackingLifecycleHandler,
        IGuildStateNotificationHandler<GuildStateSynchronized>
{
    public async Task HandleAsync(PlatformMemberUpdated platformEvent, CancellationToken cancellationToken)
    {
        if (gate.TryGetTrackedUserId(platformEvent.Member.User) is not { } userId)
        {
            Drop(platformEvent);
            return;
        }

        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        using (await nameLock.EnterAsync(cancellationToken))
        {
            // The tracking hooks change the gate before they run, so asking again under the lock keeps a member who just
            // lost tracking from getting a row.
            if (!gate.IsTracked(userId))
            {
                Drop(platformEvent);
                return;
            }

            if (await RecordAsync(guildId, userId, Names(platformEvent.Member), platformEvent.OccurredAt, cancellationToken) is not { } stored)
            {
                return;
            }

            await db.SaveChangesAsync(cancellationToken);
            metrics.Recorded(platformEvent);
            LogRecorded(stored.Kind, userId);
        }
    }

    /// <summary>
    /// Stores the baseline with the member's current names, unless their open period has a row already.
    /// </summary>
    public async Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        if (await GuildRefAsync(user.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        using (await nameLock.EnterAsync(cancellationToken))
        {
            if (await platform.GetMemberAsync(guild, user.User, cancellationToken) is not { } member)
            {
                return;
            }

            if (await RecordAsync(user.GuildId, user.UserId, Names(member), timeProvider.GetUtcNow(), cancellationToken) is { } stored)
            {
                await db.SaveChangesAsync(cancellationToken);
                LogRecorded(stored.Kind, user.UserId);
            }
        }
    }

    /// <summary>
    /// Nothing to close: a name row is a moment, not a span.
    /// </summary>
    public Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Compares every tracked member's current names with their latest row after a full guild sync (startup,
    /// reconnect) and stores a difference at the sync: when it really happened is unknown.
    /// </summary>
    public async Task HandleAsync(GuildStateSynchronized notification, CancellationToken cancellationToken)
    {
        if (await GuildRefAsync(notification.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        using (await nameLock.EnterAsync(cancellationToken))
        {
            var tracked = await db.TrackingPeriods
                .Where(p => p.GuildId == notification.GuildId && p.To == null)
                .Join(
                    db.UserPlatformIds.Where(i => i.Platform == platform.Platform),
                    p => p.UserId,
                    i => i.UserId,
                    (p, i) => new { p.UserId, i.ExternalId })
                .ToListAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var stored = 0;
            foreach (var user in tracked.Where(u => gate.IsTracked(u.UserId)))
            {
                if (await platform.GetMemberAsync(guild, new PlatformRef(platform.Platform, user.ExternalId), cancellationToken) is { } member
                    && await RecordAsync(notification.GuildId, user.UserId, Names(member), now, cancellationToken) is not null)
                {
                    stored++;
                }
            }

            if (stored > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                LogSynchronized(stored);
            }
        }
    }

    /// <summary>
    /// Adds the row <see cref="MemberNameHistory.Next"/> decides on to the context, unsaved. Nothing without an open
    /// tracking period. Call under <see cref="MemberNameLock"/>.
    /// </summary>
    private async Task<MemberNameEntry?> RecordAsync(
        Guid guildId,
        Guid userId,
        MemberNames names,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        var trackedSince = await db.TrackingPeriods
            .Where(p => p.GuildId == guildId && p.UserId == userId && p.To == null)
            .Select(p => (DateTimeOffset?)p.From)
            .FirstOrDefaultAsync(cancellationToken);
        if (trackedSince is null)
        {
            return null;
        }

        var latest = await db.MemberNameChanges
            .Where(r => r.GuildId == guildId && r.UserId == userId)
            .OrderByDescending(r => r.OccurredAt)
            .ThenByDescending(r => r.Id)
            .Select(r => new RecordedMemberNames(new MemberNames(r.Nickname, r.GlobalName, r.Username), r.OccurredAt))
            .FirstOrDefaultAsync(cancellationToken);
        if (MemberNameHistory.Next(names, observedAt, trackedSince.Value, latest) is not { } next)
        {
            return null;
        }

        db.MemberNameChanges.Add(new MemberNameChange
        {
            GuildId = guildId,
            OccurredAt = next.At,
            UserId = userId,
            Nickname = next.Names.Nickname,
            GlobalName = next.Names.GlobalName,
            Username = next.Names.Username,
            Kind = next.Kind,
        });
        return next;
    }

    private static MemberNames Names(PlatformMember member) => new(member.Nickname, member.GlobalName, member.Username);

    /// <summary>
    /// The guild on the bot's platform, to read its members; <see langword="null"/> if it has no mapping there.
    /// </summary>
    private async Task<PlatformRef?> GuildRefAsync(Guid guildId, CancellationToken cancellationToken)
    {
        var externalId = await db.GuildPlatformIds
            .Where(g => g.GuildId == guildId && g.Platform == platform.Platform)
            .Select(g => g.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);
        return externalId is null ? null : new PlatformRef(platform.Platform, externalId);
    }

    private void Drop(PlatformMemberUpdated platformEvent)
    {
        metrics.Dropped(EventDropReason.Untracked);
        LogDropped(platformEvent.Member.User);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded member names ({Kind}) for {UserId}")]
    private partial void LogRecorded(MemberNameChangeKind kind, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped member update of {User}: untracked")]
    private partial void LogDropped(PlatformRef user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored {Count} member name rows after the guild sync")]
    private partial void LogSynchronized(int count);
}
