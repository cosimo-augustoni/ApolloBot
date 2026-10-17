using Apollo.Bot.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Presence;
using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Presence;

/// <summary>
/// Records the activities of tracked users (playing, listening, ...) as <c>presence_activity_sessions</c> (#19, scope
/// §5.1). The <see cref="PresenceSessionStateMachine"/> in the shared <see cref="PresenceSessionStore"/> keeps the open
/// sessions in memory; a session is stored once it closes, and only if it lasted at least a minute.
/// </summary>
/// <remarks>
/// <para>
/// Inputs: presence updates of tracked users; the tracking hooks (gaining tracking opens sessions for the current
/// activities, also for everyone tracked after the first sync on startup; losing it closes them); every full guild sync
/// (also after a reconnect), which reconciles everyone with the current presences; and
/// <see cref="PresenceSessionLifecycleService"/> on shutdown.
/// </para>
/// <para>
/// A no-op on platforms without <see cref="PlatformCapabilities.PresenceActivities"/> (Fluxer). After a crash the open
/// sessions are lost and restart from the current presence (accepted: presence is bonus data).
/// </para>
/// </remarks>
internal sealed partial class PresenceSessionRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    IChatPlatform platform,
    PresenceSessionStore store,
    RecordedEventMetrics metrics,
    TimeProvider timeProvider,
    ILogger<PresenceSessionRecorder> logger)
    : IPlatformEventHandler<PlatformPresenceUpdated>, ITrackingLifecycleHandler, IGuildStateNotificationHandler<GuildStateSynchronized>
{
    public async Task HandleAsync(PlatformPresenceUpdated platformEvent, CancellationToken cancellationToken)
    {
        if (!platform.Capabilities.PresenceActivities)
        {
            return;
        }

        // Presence updates are frequent: the gate answers from memory, so untracked users cost no database call.
        if (gate.TryGetTrackedUserId(platformEvent.Presence.User) is not { } userId)
        {
            Drop(platformEvent);
            return;
        }

        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        var activities = PresenceActivities.From(platformEvent.Presence);

        int stored;
        using (await store.EnterAsync(cancellationToken))
        {
            // The tracking hooks run under the same lock after the gate changed, so asking again here keeps a user who
            // just lost tracking from being reopened by an event that was already on its way.
            if (!gate.IsTracked(userId))
            {
                Drop(platformEvent);
                return;
            }

            stored = await ApplyAsync(guildId, machine => machine.Update(userId, activities, platformEvent.OccurredAt), cancellationToken);
        }

        if (stored > 0)
        {
            metrics.Recorded(platformEvent);
            LogRecorded(stored, userId);
        }
    }

    public async Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        if (!platform.Capabilities.PresenceActivities || await GuildRefAsync(user.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        // The presences are read under the lock, so an update applied in the meantime is not overwritten by older ones.
        using (await store.EnterAsync(cancellationToken))
        {
            var presence = (await platform.ListPresencesAsync(guild, cancellationToken)).FirstOrDefault(p => p.User == user.User);
            var activities = presence is null ? [] : PresenceActivities.From(presence);
            var now = timeProvider.GetUtcNow();
            await ApplyAsync(user.GuildId, machine => machine.Update(user.UserId, activities, now), cancellationToken);
        }
    }

    public async Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            var now = timeProvider.GetUtcNow();
            await ApplyAsync(user.GuildId, machine => machine.Stop(user.UserId, now), cancellationToken);
        }
    }

    /// <summary>
    /// Reconciles every tracked user with the current presences after a full guild sync. On startup the
    /// <see cref="TrackingStartedAsync"/> calls already did this; after a reconnect it catches the activities that
    /// started or ended while the bot was disconnected. A session that no longer matches closes now, since the bot did
    /// not see when it really ended.
    /// </summary>
    public async Task HandleAsync(GuildStateSynchronized notification, CancellationToken cancellationToken)
    {
        if (!platform.Capabilities.PresenceActivities || await GuildRefAsync(notification.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        using (await store.EnterAsync(cancellationToken))
        {
            var current = new Dictionary<Guid, IReadOnlyList<PresenceActivity>>();
            foreach (var presence in await platform.ListPresencesAsync(guild, cancellationToken))
            {
                if (gate.TryGetTrackedUserId(presence.User) is { } userId)
                {
                    current[userId] = PresenceActivities.From(presence);
                }
            }

            var now = timeProvider.GetUtcNow();
            await ApplyAsync(notification.GuildId, machine =>
            {
                var changes = new List<PresenceChange>();
                foreach (var userId in machine.Users.Union(current.Keys).ToList())
                {
                    changes.AddRange(gate.IsTracked(userId)
                        ? machine.Update(userId, current.GetValueOrDefault(userId, []), now)
                        : machine.Stop(userId, now));
                }

                return changes;
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Closes every open session (graceful shutdown) and stores those of at least a minute. Afterwards the recorder opens
    /// nothing more, also when storing failed: hosted services stop in reverse order, so the gateway still delivers
    /// events after this.
    /// </summary>
    public async Task CloseAllAsync(CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            try
            {
                var now = timeProvider.GetUtcNow();
                await ApplyAsync(guildId: null, machine => machine.StopAll(now), cancellationToken);
            }
            finally
            {
                store.Stopped = true;
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="decide"/> on a copy of the state machine and stores the closed sessions of at least a minute
    /// in one save. The copy replaces the store's only after the save, so a failed save keeps the sessions open for a
    /// later close. Call under the store's lock. <paramref name="guildId"/> may be <see langword="null"/> only when
    /// nothing can open. After the shutdown close nothing is applied.
    /// </summary>
    /// <returns>The number of sessions stored.</returns>
    private async Task<int> ApplyAsync(
        Guid? guildId,
        Func<PresenceSessionStateMachine, IReadOnlyList<PresenceChange>> decide,
        CancellationToken cancellationToken)
    {
        if (store.Stopped)
        {
            return 0;
        }

        var machine = store.Machine.Clone();
        var changes = decide(machine);
        var rows = changes
            .OfType<PresenceSessionClosed>()
            .Where(closed => closed.IsLongEnough)
            .Select(closed => new PresenceActivitySession
            {
                GuildId = store.Guilds.TryGetValue(closed.UserId, out var sessionGuildId)
                    ? sessionGuildId
                    : guildId ?? throw new InvalidOperationException("Storing a session needs its guild."),
                OccurredAt = closed.StartedAt,
                UserId = closed.UserId,
                ActivityType = closed.Activity.Type,
                ActivityName = closed.Activity.Name,
                EndedAt = closed.At,
            })
            .ToList();
        if (rows.Count > 0)
        {
            db.PresenceActivitySessions.AddRange(rows);
            await db.SaveChangesAsync(cancellationToken);
        }

        store.Machine = machine;
        foreach (var opened in changes.OfType<PresenceSessionOpened>())
        {
            store.Guilds[opened.UserId] = guildId ?? throw new InvalidOperationException("Opening a session needs its guild.");
        }

        foreach (var userId in store.Guilds.Keys.Except(machine.Users).ToList())
        {
            store.Guilds.Remove(userId);
        }

        return rows.Count;
    }

    /// <summary>
    /// The guild on the bot's platform, to read its presences; <see langword="null"/> if it has no mapping there.
    /// </summary>
    private async Task<PlatformRef?> GuildRefAsync(Guid guildId, CancellationToken cancellationToken)
    {
        var externalId = await db.GuildPlatformIds
            .Where(g => g.GuildId == guildId && g.Platform == platform.Platform)
            .Select(g => g.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);
        return externalId is null ? null : new PlatformRef(platform.Platform, externalId);
    }

    private void Drop(PlatformPresenceUpdated platformEvent)
    {
        metrics.Dropped(EventDropReason.Untracked);
        LogDropped(platformEvent.Presence.User);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded {SessionCount} presence activity sessions for {UserId}")]
    private partial void LogRecorded(int sessionCount, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped presence update of {User}: untracked")]
    private partial void LogDropped(PlatformRef user);
}
