using Apollo.Bot.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Voice;
using Apollo.Domain.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Apollo.Bot.Voice;

/// <summary>
/// Records the voice time of tracked users as <c>voice_sessions</c> split into <c>voice_state_segments</c> (#16, scope
/// §5.1). The <see cref="VoiceSessionStateMachine"/> in the shared <see cref="VoiceSessionStore"/> decides what changes;
/// this class only feeds it and persists its output.
/// </summary>
/// <remarks>
/// <para>
/// Inputs: voice state updates of tracked users; the tracking hooks (gaining tracking opens a session from the current
/// voice state, losing it closes the session as <see cref="VoiceSessionEndReason.TrackingStopped"/>); the gateway's
/// connection state; and the reconciliation (#98).
/// </para>
/// <para>
/// The reconciliation applies one rule (<see cref="VoiceSessionStateMachine.Reconcile"/>) to every tracked member after
/// a time the bot could not see or store changes. It runs after every full guild sync (on startup and after a
/// reconnect) and every <see cref="VoiceReconciliationOptions.Interval"/> (<see cref="VoiceSessionLifecycleService"/>),
/// each with its own T, the last moment the bot knew the voice states: on startup the previous run's last connected
/// heartbeat, after a reconnect the disconnect, periodically the previous full reconciliation.
/// </para>
/// <para>
/// Before anything is applied, the sessions and segments a previous run left open are taken over into memory, so the
/// rule decides whether they continue. A graceful shutdown leaves them open.
/// </para>
/// </remarks>
internal sealed partial class VoiceSessionRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    IChatPlatform platform,
    VoiceSessionStore store,
    IOptions<VoiceReconciliationOptions> options,
    RecordedEventMetrics metrics,
    TimeProvider timeProvider,
    ILogger<VoiceSessionRecorder> logger)
    : IPlatformEventHandler<PlatformVoiceStateChanged>,
        IPlatformEventHandler<PlatformConnectionStateChanged>,
        ITrackingLifecycleHandler,
        IGuildStateNotificationHandler<GuildStateSynchronized>
{
    public async Task HandleAsync(PlatformVoiceStateChanged platformEvent, CancellationToken cancellationToken)
    {
        if (gate.TryGetTrackedUserId(platformEvent.State.User) is not { } userId)
        {
            Drop(platformEvent);
            return;
        }

        using var pending = store.BeginEvent(userId);
        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        var connection = await ToConnectionAsync(platformEvent.Guild, platformEvent.State, cancellationToken);

        IReadOnlyList<VoiceChange> changes;
        using (await store.EnterAsync(cancellationToken))
        {
            // The tracking hooks run under the same lock after the gate changed, so asking again here keeps a user who
            // just lost tracking from being reopened by an event that was already on its way.
            if (!gate.IsTracked(userId))
            {
                Drop(platformEvent);
                return;
            }

            if (InBlindSpot && !store.ReconciledInBlindSpot.Contains(userId))
            {
                // The first event of a user the rule has not run for yet in this blind spot (the gate published its
                // snapshot before the user's tracking start hook, or the reconnect's sync has not run yet): the event's
                // state is their current state.
                var blindSpot = await BlindSpotAsync(guildId, platformEvent.OccurredAt, cancellationToken);
                changes = await ApplyAsync(guildId, machine => machine.Reconcile(userId, [connection], blindSpot), cancellationToken);
                store.ReconciledInBlindSpot.Add(userId);
            }
            else
            {
                changes = await ApplyAsync(guildId, machine => machine.Update(userId, connection, platformEvent.OccurredAt), cancellationToken);
            }
        }

        if (changes.Count > 0)
        {
            metrics.Recorded(platformEvent);
            LogRecorded(changes.Count, userId);
        }
    }

    /// <summary>
    /// Remembers when the gateway disconnected (T for the reconciliation after the reconnect) and whether it is down. A
    /// resume replays the missed events, so it ends the blind spot without a reconciliation.
    /// </summary>
    public async Task HandleAsync(PlatformConnectionStateChanged platformEvent, CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            switch (platformEvent.State)
            {
                case PlatformConnectionState.Disconnected:
                    store.GatewayDown = true;
                    if (store.DisconnectedAt is null)
                    {
                        store.DisconnectedAt = platformEvent.OccurredAt;
                        store.SyncedSinceDisconnect = false;
                        store.ReconciledInBlindSpot.Clear();
                    }

                    break;
                case PlatformConnectionState.Resumed:
                    store.GatewayDown = false;
                    store.DisconnectedAt = null;
                    break;
                default:
                    store.GatewayDown = false;
                    break;
            }
        }
    }

    /// <summary>
    /// Applies the rule to the user with the current blind spot: on startup (the gate's first <c>TrackingStartedAsync</c>
    /// calls) it decides whether a taken-over session continues; later it opens a session from the current voice state.
    /// </summary>
    public async Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        if (await GuildRefAsync(user.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        // The voice states are read under the lock, so an event applied in the meantime is not overwritten by older ones.
        using (await store.EnterAsync(cancellationToken))
        {
            await EnsureTakenOverAsync(cancellationToken);
            var blindSpot = await BlindSpotAsync(user.GuildId, timeProvider.GetUtcNow(), cancellationToken);
            var states = await platform.ListVoiceStatesAsync(guild, cancellationToken);
            var connections = await ToConnectionsAsync(guild, states.Where(s => s.User == user.User), cancellationToken);
            await ApplyAsync(user.GuildId, machine => machine.Reconcile(user.UserId, connections, blindSpot), cancellationToken);
            if (InBlindSpot)
            {
                store.ReconciledInBlindSpot.Add(user.UserId);
            }
        }
    }

    public async Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            var now = timeProvider.GetUtcNow();
            await ApplyAsync(user.GuildId, machine => machine.Stop(user.UserId, now, VoiceSessionEndReason.TrackingStopped), cancellationToken);
        }
    }

    /// <summary>
    /// Reconciles everyone after a full guild sync: on startup with T = the previous run's last heartbeat, after a
    /// reconnect with T = the disconnect.
    /// </summary>
    public Task HandleAsync(GuildStateSynchronized notification, CancellationToken cancellationToken) =>
        ReconcileAsync(notification.GuildId, cancellationToken);

    /// <summary>
    /// The periodic run: reconciles everyone in the guild of the latest full sync. While the gateway is down, after a
    /// reconnect until its sync, and while the guild is unavailable it waits (the cache may be empty); members with a
    /// voice event on its way are left to that event.
    /// </summary>
    public Task ReconcileAsync(CancellationToken cancellationToken) => ReconcileAsync(guildId: null, cancellationToken);

    /// <summary>
    /// Records the start of this run and takes over the sessions a previous run left open (startup).
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            store.StartedAt ??= timeProvider.GetUtcNow();
            await EnsureTakenOverAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Reconciles everyone in <paramref name="guildId"/>, the synced guild, or for the periodic run (<see langword="null"/>)
    /// in the guild of the latest sync.
    /// </summary>
    private async Task ReconcileAsync(Guid? guildId, CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            var periodic = guildId is null;
            if (guildId is not null)
            {
                // Remembered before the run, so the periodic run retries a reconciliation after a sync that failed.
                store.GuildId = guildId;
                store.SyncedSinceDisconnect = true;
            }

            // Before the gate's first rebuild everyone would look untracked; the gate's TrackingStartedAsync calls and
            // the next periodic run reconcile once it is there.
            if ((guildId ?? store.GuildId) is not { } id || gate.Mode is null)
            {
                return;
            }

            if (periodic && store.GatewayDown)
            {
                LogSkippedWhileDisconnected();
                return;
            }

            if (periodic && store.DisconnectedAt is not null && !store.SyncedSinceDisconnect)
            {
                LogSkippedBeforeSync();
                return;
            }

            if (await GuildRefAsync(id, cancellationToken) is not { } guild)
            {
                return;
            }

            // An empty cache would look like everyone left.
            if (periodic && !platform.IsGuildAvailable(guild))
            {
                LogSkippedGuildUnavailable();
                return;
            }

            await EnsureTakenOverAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var trigger = !store.StartupReconciled ? "startup" : store.DisconnectedAt is not null ? "reconnect" : periodic ? "periodic" : "sync";
            var blindSpot = await BlindSpotAsync(id, now, cancellationToken);

            // Only while the gateway stayed connected can a member's voice state differ because an event is still on its
            // way; after a blind spot the current state is the baseline the event then applies to.
            var skipPending = periodic && trigger == "periodic";

            var current = new Dictionary<Guid, IReadOnlyList<VoiceConnection>>();
            foreach (var userStates in (await platform.ListVoiceStatesAsync(guild, cancellationToken)).GroupBy(s => s.User))
            {
                if (gate.TryGetTrackedUserId(userStates.Key) is { } userId)
                {
                    current[userId] = await ToConnectionsAsync(guild, userStates, cancellationToken);
                }
            }

            var changes = await ApplyAsync(id, machine =>
            {
                var reconciled = new List<VoiceChange>();
                foreach (var userId in machine.UsersInVoice.Union(current.Keys).ToList())
                {
                    if (skipPending && store.HasPendingEvents(userId))
                    {
                        continue;
                    }

                    reconciled.AddRange(gate.IsTracked(userId)
                        ? machine.Reconcile(userId, current.GetValueOrDefault(userId, []), blindSpot)
                        : machine.ReconcileUntracked(userId, blindSpot));
                }

                return reconciled;
            }, cancellationToken);

            store.StartupReconciled = true;
            store.DisconnectedAt = null;
            store.ReconciledInBlindSpot.Clear();
            store.LastReconciledAt = now;
            if (changes.Count > 0)
            {
                LogReconciled(changes.Count, trigger, blindSpot.LastKnownAt);
            }
        }
    }

    /// <summary>
    /// Whether a blind spot is open that a full reconciliation has not closed yet: the startup one, or a disconnect.
    /// </summary>
    private bool InBlindSpot => !store.StartupReconciled || store.DisconnectedAt is not null;

    /// <summary>
    /// The blind spot for a reconciliation at <paramref name="now"/>: on startup since the previous run's last connected
    /// heartbeat (none: every session closes at the start), after a disconnect since the disconnect, otherwise since the
    /// previous full reconciliation, with the gateway connected. Call under the store's lock.
    /// </summary>
    private async Task<VoiceBlindSpot> BlindSpotAsync(Guid guildId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var maxGap = options.Value.MaxGap;
        var startedAt = store.StartedAt ?? now;
        if (!store.StartupReconciled)
        {
            if (store.StartupHeartbeat is not { } heartbeat || heartbeat.GuildId != guildId)
            {
                var at = await db.BotStatuses
                    .Where(b => b.GuildId == guildId && b.Connected && b.OccurredAt < startedAt)
                    .MaxAsync(b => (DateTimeOffset?)b.OccurredAt, cancellationToken);
                heartbeat = store.StartupHeartbeat = new StartupHeartbeat(guildId, at);
            }

            return heartbeat.At is { } lastKnownAt
                ? VoiceBlindSpot.Outage(lastKnownAt, now, maxGap)
                : VoiceBlindSpot.Unknown(startedAt, now);
        }

        return store.DisconnectedAt is { } disconnectedAt
            ? VoiceBlindSpot.Outage(disconnectedAt, now, maxGap)
            : VoiceBlindSpot.WhileConnected(store.LastReconciledAt ?? startedAt, now);
    }

    /// <summary>
    /// Runs <paramref name="decide"/> on a copy of the state machine and stores its changes in one transaction. The copy
    /// and the new row keys replace the store's only after the commit, so a failed save changes nothing. Call under the
    /// store's lock. <paramref name="guildId"/> is the guild new sessions open in.
    /// </summary>
    private async Task<IReadOnlyList<VoiceChange>> ApplyAsync(
        Guid guildId,
        Func<VoiceSessionStateMachine, IReadOnlyList<VoiceChange>> decide,
        CancellationToken cancellationToken)
    {
        await EnsureTakenOverAsync(cancellationToken);

        var machine = store.Machine.Clone();
        var changes = decide(machine);
        if (changes.Count == 0)
        {
            store.Machine = machine;
            return changes;
        }

        var rows = new Dictionary<Guid, OpenVoiceRows?>();
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            foreach (var change in changes)
            {
                var open = rows.TryGetValue(change.UserId, out var pending) ? pending : store.Rows.GetValueOrDefault(change.UserId);
                rows[change.UserId] = change switch
                {
                    VoiceSessionOpened opened => await OpenAsync(guildId, opened, cancellationToken),
                    VoiceSessionClosed closed => await CloseAsync(open, closed, cancellationToken),
                    VoiceSegmentChanged segment => await ChangeSegmentAsync(open, segment, cancellationToken),
                    _ => throw new InvalidOperationException($"Unknown voice change {change.GetType().Name}."),
                };
            }

            await transaction.CommitAsync(cancellationToken);
        }

        store.Machine = machine;
        foreach (var (userId, open) in rows)
        {
            if (open is null)
            {
                store.Rows.Remove(userId);
            }
            else
            {
                store.Rows[userId] = open;
            }
        }

        return changes;
    }

    private async Task<OpenVoiceRows> OpenAsync(Guid guildId, VoiceSessionOpened opened, CancellationToken cancellationToken)
    {
        var session = new VoiceSession
        {
            GuildId = guildId,
            OccurredAt = opened.At,
            UserId = opened.UserId,
            ChannelId = opened.ChannelId,
        };
        db.VoiceSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var segment = await AddSegmentAsync(guildId, session.Id, opened.UserId, opened.At, opened.Flags, cancellationToken);
        return new OpenVoiceRows(guildId, session.Id, session.OccurredAt, segment.Id, segment.OccurredAt);
    }

    private async Task<OpenVoiceRows?> CloseAsync(OpenVoiceRows? open, VoiceSessionClosed closed, CancellationToken cancellationToken)
    {
        if (open is null)
        {
            LogNoOpenRows(closed.UserId);
            return null;
        }

        await CloseSegmentAsync(open, closed.At, cancellationToken);
        await db.VoiceSessions
            .Where(s => s.Id == open.SessionId && s.OccurredAt == open.SessionStartedAt)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.EndedAt, closed.At).SetProperty(x => x.EndReason, closed.Reason),
                cancellationToken);
        return null;
    }

    private async Task<OpenVoiceRows?> ChangeSegmentAsync(OpenVoiceRows? open, VoiceSegmentChanged changed, CancellationToken cancellationToken)
    {
        if (open is null)
        {
            LogNoOpenRows(changed.UserId);
            return null;
        }

        await CloseSegmentAsync(open, changed.At, cancellationToken);
        var segment = await AddSegmentAsync(open.GuildId, open.SessionId, changed.UserId, changed.At, changed.Flags, cancellationToken);
        return open with { SegmentId = segment.Id, SegmentStartedAt = segment.OccurredAt };
    }

    private Task CloseSegmentAsync(OpenVoiceRows open, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.VoiceStateSegments
            .Where(s => s.Id == open.SegmentId && s.OccurredAt == open.SegmentStartedAt)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, at), cancellationToken);

    private async Task<VoiceStateSegment> AddSegmentAsync(
        Guid guildId,
        long sessionId,
        Guid userId,
        DateTimeOffset at,
        VoiceFlags flags,
        CancellationToken cancellationToken)
    {
        var segment = new VoiceStateSegment
        {
            GuildId = guildId,
            OccurredAt = at,
            SessionId = sessionId,
            UserId = userId,
            SelfMute = flags.SelfMute,
            SelfDeaf = flags.SelfDeaf,
            Streaming = flags.Streaming,
            Video = flags.Video,
        };
        db.VoiceStateSegments.Add(segment);
        await db.SaveChangesAsync(cancellationToken);
        return segment;
    }

    /// <summary>
    /// Takes over, once per process, the sessions a previous run left open: per user the newest open session with its
    /// newest open segment goes into the state machine, so the first reconciliation decides whether it continues. Any
    /// other open row (a session without an open segment, an older duplicate) cannot be continued and closes as
    /// <see cref="VoiceSessionEndReason.Unobserved"/> at the start of this run. Call under the store's lock.
    /// </summary>
    private async Task EnsureTakenOverAsync(CancellationToken cancellationToken)
    {
        if (store.TakenOver)
        {
            return;
        }

        var startedAt = store.StartedAt ??= timeProvider.GetUtcNow();
        var sessions = await db.VoiceSessions.AsNoTracking().Where(s => s.EndedAt == null).ToListAsync(cancellationToken);
        var segments = await db.VoiceStateSegments.AsNoTracking().Where(s => s.EndedAt == null).ToListAsync(cancellationToken);

        var machine = new VoiceSessionStateMachine();
        var rows = new Dictionary<Guid, OpenVoiceRows>();
        foreach (var session in sessions.GroupBy(s => s.UserId).Select(g => g.OrderByDescending(s => s.OccurredAt).ThenByDescending(s => s.Id).First()))
        {
            var segment = segments
                .Where(s => s.SessionId == session.Id && s.OccurredAt >= session.OccurredAt)
                .OrderByDescending(s => s.OccurredAt)
                .ThenByDescending(s => s.Id)
                .FirstOrDefault();
            if (segment is null)
            {
                continue;
            }

            machine.Restore(session.UserId, session.ChannelId, new VoiceFlags(segment.SelfMute, segment.SelfDeaf, segment.Streaming, segment.Video), segment.OccurredAt);
            rows[session.UserId] = new OpenVoiceRows(session.GuildId, session.Id, session.OccurredAt, segment.Id, segment.OccurredAt);
        }

        var staleSessions = sessions.Where(s => !rows.Values.Any(r => r.SessionId == s.Id)).Select(s => s.Id).ToList();
        var staleSegments = segments.Where(s => !rows.Values.Any(r => r.SegmentId == s.Id)).Select(s => s.Id).ToList();
        if (staleSessions.Count > 0 || staleSegments.Count > 0)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.VoiceStateSegments
                .Where(s => staleSegments.Contains(s.Id) && s.EndedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, startedAt), cancellationToken);
            await db.VoiceSessions
                .Where(s => staleSessions.Contains(s.Id) && s.EndedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.EndedAt, startedAt).SetProperty(x => x.EndReason, VoiceSessionEndReason.Unobserved),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LogClosedStale(staleSessions.Count, staleSegments.Count);
        }

        store.Machine = machine;
        store.Rows.Clear();
        foreach (var (userId, open) in rows)
        {
            store.Rows[userId] = open;
        }

        store.TakenOver = true;
        LogTookOver(rows.Count);
    }

    private async Task<IReadOnlyList<VoiceConnection>> ToConnectionsAsync(
        PlatformRef guild,
        IEnumerable<PlatformVoiceState> states,
        CancellationToken cancellationToken)
    {
        var connections = new List<VoiceConnection>();
        foreach (var state in states)
        {
            connections.Add(await ToConnectionAsync(guild, state, cancellationToken));
        }

        return connections;
    }

    private async Task<VoiceConnection> ToConnectionAsync(PlatformRef guild, PlatformVoiceState state, CancellationToken cancellationToken) =>
        new(
            state.ConnectionId,
            state.Channel is { } channel ? await ids.ResolveChannelAsync(guild, channel, cancellationToken) : null,
            new VoiceFlags(state.SelfMute, state.SelfDeaf, state.Streaming, state.Video));

    /// <summary>
    /// The guild on the bot's platform, to read its voice states; <see langword="null"/> if it has no mapping there.
    /// </summary>
    private async Task<PlatformRef?> GuildRefAsync(Guid guildId, CancellationToken cancellationToken)
    {
        var externalId = await db.GuildPlatformIds
            .Where(g => g.GuildId == guildId && g.Platform == platform.Platform)
            .Select(g => g.ExternalId)
            .FirstOrDefaultAsync(cancellationToken);
        return externalId is null ? null : new PlatformRef(platform.Platform, externalId);
    }

    private void Drop(PlatformVoiceStateChanged platformEvent)
    {
        metrics.Dropped(EventDropReason.Untracked);
        LogDropped(platformEvent.State.User);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded {ChangeCount} voice changes for {UserId}")]
    private partial void LogRecorded(int changeCount, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped voice state update of {User}: untracked")]
    private partial void LogDropped(PlatformRef user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Took over {SessionCount} voice sessions left open by the previous run")]
    private partial void LogTookOver(int sessionCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Closed {SessionCount} voice sessions and {SegmentCount} segments left open that could not be taken over as Unobserved")]
    private partial void LogClosedStale(int sessionCount, int segmentCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Voice reconciliation ({Trigger}) stored {ChangeCount} changes; last known state at {LastKnownAt}")]
    private partial void LogReconciled(int changeCount, string trigger, DateTimeOffset lastKnownAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped the periodic voice reconciliation: the gateway is disconnected, the reconnect reconciles")]
    private partial void LogSkippedWhileDisconnected();

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped the periodic voice reconciliation: connected again, the reconnect's sync reconciles")]
    private partial void LogSkippedBeforeSync();

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped the periodic voice reconciliation: the guild is unavailable")]
    private partial void LogSkippedGuildUnavailable();

    [LoggerMessage(Level = LogLevel.Warning, Message = "No open voice session rows for {UserId}, probably after a failed save; skipping the change")]
    private partial void LogNoOpenRows(Guid userId);
}
