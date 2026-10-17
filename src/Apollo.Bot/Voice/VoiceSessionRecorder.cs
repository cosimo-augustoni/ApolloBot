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

namespace Apollo.Bot.Voice;

/// <summary>
/// Records the voice time of tracked users as <c>voice_sessions</c> split into <c>voice_state_segments</c> (#16, scope
/// §5.1). The <see cref="VoiceSessionStateMachine"/> in the shared <see cref="VoiceSessionStore"/> decides what changes;
/// this class only feeds it and persists its output.
/// </summary>
/// <remarks>
/// <para>
/// Inputs: voice state updates of tracked users; the tracking hooks (gaining tracking opens a session from the current
/// voice state, losing it closes the session as <see cref="VoiceSessionEndReason.TrackingStopped"/>); every full guild
/// sync (on startup and after a reconnect), which reconciles everyone with the current voice states; and
/// <see cref="VoiceSessionLifecycleService"/> on startup and shutdown.
/// </para>
/// <para>
/// Before anything is opened, the sessions and segments a previous run left open are closed as
/// <see cref="VoiceSessionEndReason.BotRestart"/> at that moment, whichever input comes first.
/// </para>
/// </remarks>
internal sealed partial class VoiceSessionRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    IChatPlatform platform,
    VoiceSessionStore store,
    RecordedEventMetrics metrics,
    TimeProvider timeProvider,
    ILogger<VoiceSessionRecorder> logger)
    : IPlatformEventHandler<PlatformVoiceStateChanged>, ITrackingLifecycleHandler, IGuildStateNotificationHandler<GuildStateSynchronized>
{
    public async Task HandleAsync(PlatformVoiceStateChanged platformEvent, CancellationToken cancellationToken)
    {
        if (gate.TryGetTrackedUserId(platformEvent.State.User) is not { } userId)
        {
            Drop(platformEvent);
            return;
        }

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

            changes = await ApplyAsync(guildId, machine => machine.Update(userId, connection, platformEvent.OccurredAt), cancellationToken);
        }

        if (changes.Count > 0)
        {
            metrics.Recorded(platformEvent);
            LogRecorded(changes.Count, userId);
        }
    }

    public async Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        if (await GuildRefAsync(user.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        // The voice states are read under the lock, so an event applied in the meantime is not overwritten by older ones.
        using (await store.EnterAsync(cancellationToken))
        {
            var states = await platform.ListVoiceStatesAsync(guild, cancellationToken);
            var connections = await ToConnectionsAsync(guild, states.Where(s => s.User == user.User), cancellationToken);
            var now = timeProvider.GetUtcNow();
            await ApplyAsync(user.GuildId, machine => machine.Reconcile(user.UserId, connections, now, VoiceSessionEndReason.BotRestart), cancellationToken);
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
    /// Reconciles every tracked user with the current voice states after a full guild sync. On startup the
    /// <see cref="TrackingStartedAsync"/> calls already did this; after a reconnect it catches the joins, leaves and
    /// moves the bot missed. A session that no longer matches closes as <see cref="VoiceSessionEndReason.BotRestart"/>,
    /// since the bot did not see when it really ended.
    /// </summary>
    public async Task HandleAsync(GuildStateSynchronized notification, CancellationToken cancellationToken)
    {
        if (await GuildRefAsync(notification.GuildId, cancellationToken) is not { } guild)
        {
            return;
        }

        using (await store.EnterAsync(cancellationToken))
        {
            var current = new Dictionary<Guid, IReadOnlyList<VoiceConnection>>();
            foreach (var userStates in (await platform.ListVoiceStatesAsync(guild, cancellationToken)).GroupBy(s => s.User))
            {
                if (gate.TryGetTrackedUserId(userStates.Key) is { } userId)
                {
                    current[userId] = await ToConnectionsAsync(guild, userStates, cancellationToken);
                }
            }

            var now = timeProvider.GetUtcNow();
            await ApplyAsync(notification.GuildId, machine =>
            {
                var changes = new List<VoiceChange>();
                foreach (var userId in machine.UsersInVoice.Union(current.Keys).ToList())
                {
                    changes.AddRange(gate.IsTracked(userId)
                        ? machine.Reconcile(userId, current.GetValueOrDefault(userId, []), now, VoiceSessionEndReason.BotRestart)
                        : machine.Stop(userId, now, VoiceSessionEndReason.TrackingStopped));
                }

                return changes;
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Closes the sessions and segments a previous run left open (startup).
    /// </summary>
    public async Task CloseLeftOpenSessionsAsync(CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            await EnsureLeftOpenSessionsClosedAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Closes every open session as <see cref="VoiceSessionEndReason.BotRestart"/> (graceful shutdown). Afterwards the
    /// recorder opens nothing more, also when the close failed: hosted services stop in reverse order, so the gateway
    /// still delivers events after this.
    /// </summary>
    public async Task CloseAllAsync(CancellationToken cancellationToken)
    {
        using (await store.EnterAsync(cancellationToken))
        {
            try
            {
                var now = timeProvider.GetUtcNow();
                await ApplyAsync(guildId: null, machine => machine.StopAll(now, VoiceSessionEndReason.BotRestart), cancellationToken);
            }
            finally
            {
                store.Stopped = true;
            }
        }
    }

    /// <summary>
    /// Runs <paramref name="decide"/> on a copy of the state machine and stores its changes in one transaction. The copy
    /// and the new row keys replace the store's only after the commit, so a failed save changes nothing. Call under the
    /// store's lock. <paramref name="guildId"/> may be <see langword="null"/> only when nothing can open. After the
    /// shutdown close nothing is applied.
    /// </summary>
    private async Task<IReadOnlyList<VoiceChange>> ApplyAsync(
        Guid? guildId,
        Func<VoiceSessionStateMachine, IReadOnlyList<VoiceChange>> decide,
        CancellationToken cancellationToken)
    {
        if (store.Stopped)
        {
            return [];
        }

        await EnsureLeftOpenSessionsClosedAsync(cancellationToken);

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
                    VoiceSessionOpened opened => await OpenAsync(guildId ?? throw new InvalidOperationException("Opening a session needs its guild."), opened, cancellationToken),
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
    /// Closes, once per process, what a previous run left open, at the current time (the real end is unknown). Call
    /// under the store's lock.
    /// </summary>
    private async Task EnsureLeftOpenSessionsClosedAsync(CancellationToken cancellationToken)
    {
        if (store.LeftOpenSessionsClosed)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        int sessions;
        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            await db.VoiceStateSegments
                .Where(s => s.EndedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EndedAt, now), cancellationToken);
            sessions = await db.VoiceSessions
                .Where(s => s.EndedAt == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.EndedAt, now).SetProperty(x => x.EndReason, VoiceSessionEndReason.BotRestart),
                    cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        store.LeftOpenSessionsClosed = true;
        LogClosedLeftOpen(sessions);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Closed {SessionCount} voice sessions left open by the previous run as BotRestart")]
    private partial void LogClosedLeftOpen(int sessionCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No open voice session rows for {UserId}, probably after a failed save; skipping the change")]
    private partial void LogNoOpenRows(Guid userId);
}
