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
/// sessions in memory; a session is stored once it closes, and only if it lasted at least a minute, together with all of
/// its <c>presence_activity_segments</c> (#115, rich presence) in the same save.
/// </summary>
/// <remarks>
/// <para>
/// Inputs: presence updates of tracked users; the tracking hooks (gaining tracking opens sessions for the current
/// activities, also for everyone tracked after the first sync on startup; losing it closes them); every full guild
/// sync (also after a reconnect), which reconciles everyone with the current presences and closes a mismatch now; the
/// gateway's connection state; and <see cref="PresenceSessionLifecycleService"/>: the periodic run and the shutdown
/// close.
/// </para>
/// <para>
/// The periodic run (#98, every <see cref="Reconciliation.ReconciliationOptions.Interval"/>) corrects a failed save: a
/// session whose activity is gone closes at the previous full reconciliation (never before its start), so an activity
/// never runs on until the member's next presence update; a current activity without a session opens one from now;
/// matching sessions stay untouched. It waits while the gateway is down, after a reconnect until its sync, and while the
/// guild is unavailable, and leaves members whose update is on its way to that update.
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
    : IPlatformEventHandler<PlatformPresenceUpdated>,
        IPlatformEventHandler<PlatformConnectionStateChanged>,
        ITrackingLifecycleHandler,
        IGuildStateNotificationHandler<GuildStateSynchronized>
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

        using var pending = store.BeginEvent(userId);
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

    /// <summary>
    /// Remembers whether the gateway is down and whether a reconnect still waits for its sync, for the periodic run.
    /// </summary>
    public async Task HandleAsync(PlatformConnectionStateChanged platformEvent, CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            switch (platformEvent.State)
            {
                case PlatformConnectionState.Disconnected:
                    store.GatewayDown = true;
                    store.AwaitingSync = true;
                    break;
                case PlatformConnectionState.Resumed:
                    // Missed updates are replayed, so no sync is needed.
                    store.GatewayDown = false;
                    store.AwaitingSync = false;
                    break;
                default:
                    store.GatewayDown = false;
                    break;
            }
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
            store.GuildId = notification.GuildId;
            store.AwaitingSync = false;
            var current = new Dictionary<Guid, IReadOnlyList<ObservedActivity>>();
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
            store.LastReconciledAt = now;
        }
    }

    /// <summary>
    /// Records the start of this run: T for the first periodic run.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            store.StartedAt ??= timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// The periodic run (#98): compares every tracked member's open sessions with their current presence, while the
    /// gateway stayed connected, so only a failed save can have been missed.
    /// </summary>
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!platform.Capabilities.PresenceActivities)
        {
            return;
        }

        using (await store.EnterAsync(cancellationToken))
        {
            if (store.Stopped || store.GuildId is not { } guildId || gate.Mode is null)
            {
                return;
            }

            if (store.GatewayDown || store.AwaitingSync)
            {
                LogSkippedWhileReconnecting();
                return;
            }

            if (await GuildRefAsync(guildId, cancellationToken) is not { } guild)
            {
                return;
            }

            // An empty cache would look like every activity ended.
            if (!platform.IsGuildAvailable(guild))
            {
                LogSkippedGuildUnavailable();
                return;
            }

            var now = timeProvider.GetUtcNow();
            var lastKnownAt = store.LastReconciledAt ?? store.StartedAt ?? now;
            var current = new Dictionary<Guid, IReadOnlyList<ObservedActivity>>();
            foreach (var presence in await platform.ListPresencesAsync(guild, cancellationToken))
            {
                if (gate.TryGetTrackedUserId(presence.User) is { } userId)
                {
                    current[userId] = PresenceActivities.From(presence);
                }
            }

            var stored = await ApplyAsync(guildId, machine =>
            {
                var changes = new List<PresenceChange>();
                foreach (var userId in machine.Users.Union(current.Keys).ToList())
                {
                    if (store.HasPendingEvents(userId))
                    {
                        continue;
                    }

                    changes.AddRange(gate.IsTracked(userId)
                        ? machine.Reconcile(userId, current.GetValueOrDefault(userId, []), lastKnownAt, now)
                        : machine.Stop(userId, now));
                }

                return changes;
            }, cancellationToken);
            store.LastReconciledAt = now;
            if (stored > 0)
            {
                LogReconciled(stored, lastKnownAt);
            }
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
        var closedSessions = changes.OfType<PresenceSessionClosed>().Where(closed => closed.IsLongEnough).ToList();
        foreach (var closed in closedSessions)
        {
            var sessionGuildId = store.Guilds.TryGetValue(closed.UserId, out var openedIn)
                ? openedIn
                : guildId ?? throw new InvalidOperationException("Storing a session needs its guild.");
            db.PresenceActivitySessions.Add(new PresenceActivitySession
            {
                GuildId = sessionGuildId,
                OccurredAt = closed.StartedAt,
                UserId = closed.UserId,
                ActivityType = closed.Activity.Type,
                ActivityName = closed.Activity.Name,
                EndedAt = closed.At,
            });
            db.PresenceActivitySegments.AddRange(closed.Segments.Select(segment => ToRow(sessionGuildId, closed, segment)));
        }

        // One save, so a session and its segments are stored together or not at all.
        if (closedSessions.Count > 0)
        {
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

        return closedSessions.Count;
    }

    private static PresenceActivitySegment ToRow(Guid guildId, PresenceSessionClosed closed, PresenceSegment segment) => new()
    {
        GuildId = guildId,
        OccurredAt = segment.From,
        UserId = closed.UserId,
        ActivityType = closed.Activity.Type,
        ActivityName = closed.Activity.Name,
        EndedAt = segment.To,
        Details = segment.Details.Details,
        State = segment.Details.State,
        LargeText = segment.Details.LargeText,
        SmallText = segment.Details.SmallText,
        PartyExtId = segment.Details.PartyId,
        PartySize = segment.Details.PartySize,
        PartyMax = segment.Details.PartyMax,
        ApplicationExtId = segment.Details.ApplicationId,
        ReportedStart = segment.Details.ReportedStart,
        ReportedEnd = segment.Details.ReportedEnd,
    };

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Periodic presence reconciliation stored {SessionCount} sessions ending at {LastKnownAt}")]
    private partial void LogReconciled(int sessionCount, DateTimeOffset lastKnownAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped the periodic presence reconciliation: the gateway is disconnected or its reconnect's sync has not run yet")]
    private partial void LogSkippedWhileReconnecting();

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped the periodic presence reconciliation: the guild is unavailable")]
    private partial void LogSkippedGuildUnavailable();
}
