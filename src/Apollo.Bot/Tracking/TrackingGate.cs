using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Tracking;

/// <summary>
/// The <see cref="ITrackingGate"/> (#14): an in-memory <see cref="TrackingSnapshot"/> of who is tracked, rebuilt from the
/// database by <see cref="ReloadAsync(Guid, CancellationToken)"/> (after every full guild sync and every 30 s) and
/// patched by <see cref="ReevaluateMemberAsync"/> (after every member change). Every rebuild compares the old and the new
/// tracked set, brings <c>tracking_periods</c> in line and calls the <see cref="ITrackingLifecycleHandler"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Reads are lock-free: the snapshot is replaced as a whole. Rebuilds are serialised, so each one reads the state the
/// previous one committed and the hooks see the changes of one user in order.
/// </para>
/// <para>
/// The first rebuild starts from an empty tracked set: it opens periods for tracked users without one, closes the
/// periods left open by users who are no longer tracked, and calls <see cref="ITrackingLifecycleHandler.TrackingStartedAsync"/>
/// for every tracked user, so session recorders can reconcile after a restart.
/// </para>
/// <para>
/// A data deletion (#22) keeps a user untracked with <see cref="MarkUntrackedAsync"/> while the rule still tracks them,
/// until the removal of their tracking role is synced or the deletion releases the mark.
/// </para>
/// <para>
/// A recorder that asked <see cref="IsTracked"/> just before a change may still store an event right after the user's
/// period closed (or skip one right after it opened). The gate decision at processing time wins; such in-flight events
/// are not reconciled with the period or with session closes.
/// </para>
/// </remarks>
internal sealed partial class TrackingGate(
    IServiceScopeFactory scopeFactory,
    IChatPlatform platform,
    TimeProvider timeProvider,
    ILogger<TrackingGate> logger) : ITrackingGate, IDisposable
{
    private static readonly IReadOnlyDictionary<Guid, PlatformRef> _nobody = new Dictionary<Guid, PlatformRef>();

    private readonly SemaphoreSlim _rebuild = new(1, 1);

    // Users kept untracked while their data is deleted (MarkUntrackedAsync). Read and written only under _rebuild.
    private readonly HashSet<Guid> _untracked = [];

    private volatile TrackingSnapshot? _snapshot;

    // The guild of the latest full sync, remembered before its reload runs, so the 30 s reloader retries a startup
    // reload that failed. Read and written only under _rebuild.
    private Guid? _guildId;

    public ITrackingView Current => _snapshot ?? TrackingSnapshot.None;

    public TrackingMode? Mode => Current.Mode;

    public PlatformRef? TrackingRole => Current.TrackingRole;

    public bool IsTracked(Guid userId) => Current.IsTracked(userId);

    public Guid? TryGetTrackedUserId(PlatformRef user) => Current.TryGetTrackedUserId(user);

    /// <summary>
    /// Rebuilds the snapshot of the guild of the latest full sync, also when its reload failed so far; does nothing
    /// before the first guild sync.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _rebuild.WaitAsync(cancellationToken);
        try
        {
            if (_guildId is { } guildId)
            {
                await RebuildAsync(guildId, cancellationToken);
            }
        }
        finally
        {
            _rebuild.Release();
        }
    }

    /// <summary>
    /// Rebuilds the snapshot from the role setting and every current member of <paramref name="guildId"/>.
    /// </summary>
    public async Task ReloadAsync(Guid guildId, CancellationToken cancellationToken)
    {
        await _rebuild.WaitAsync(cancellationToken);
        try
        {
            _guildId = guildId;
            await RebuildAsync(guildId, cancellationToken);
        }
        finally
        {
            _rebuild.Release();
        }
    }

    /// <summary>
    /// Re-evaluates one member with the current role setting. Before the first guild sync it does nothing: the reload
    /// after that sync covers every member.
    /// </summary>
    public async Task ReevaluateMemberAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        await _rebuild.WaitAsync(cancellationToken);
        try
        {
            await ReevaluateAsync(guildId, userId, cancellationToken);
        }
        finally
        {
            _rebuild.Release();
        }
    }

    /// <summary>
    /// Stops tracking <paramref name="userId"/> at once, although the rule may still track them, for a data deletion
    /// (#22): the period closes and the <see cref="ITrackingLifecycleHandler.TrackingStoppedAsync"/> hooks run, as for any
    /// loss of tracking, so events on their way are dropped. The mark ends by itself once a rebuild or member
    /// re-evaluation finds the rule no longer tracks the user (the removal of their tracking role is synced), or with
    /// <see cref="ReleaseUntrackedMarkAsync"/>.
    /// </summary>
    public async Task MarkUntrackedAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        await _rebuild.WaitAsync(cancellationToken);
        try
        {
            _untracked.Add(userId);
            if (_snapshot is not { } current || current.GuildId != guildId || !current.Tracked.ContainsKey(userId))
            {
                return;
            }

            var tracked = new Dictionary<Guid, PlatformRef>(current.Tracked);
            tracked.Remove(userId);
            await using var scope = scopeFactory.CreateAsyncScope();
            await ApplyAsync(
                scope.ServiceProvider,
                new TrackingSnapshot(guildId, current.Settings, current.TrackingRoleName, current.TrackingRole, tracked),
                userId,
                cancellationToken);
        }
        finally
        {
            _rebuild.Release();
        }
    }

    /// <summary>
    /// Ends the mark of <see cref="MarkUntrackedAsync"/> and re-evaluates the member at once: a user the rule still
    /// tracks (bootstrap mode, or a tracking role that could not be removed) is tracked again from now, with a new period.
    /// </summary>
    public async Task ReleaseUntrackedMarkAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        await _rebuild.WaitAsync(cancellationToken);
        try
        {
            _untracked.Remove(userId);
            await ReevaluateAsync(guildId, userId, cancellationToken);
        }
        finally
        {
            _rebuild.Release();
        }
    }

    public void Dispose() => _rebuild.Dispose();

    /// <remarks>Call under <c>_rebuild</c>.</remarks>
    private async Task ReevaluateAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        if (_snapshot is not { } current || current.GuildId != guildId)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var loader = scope.ServiceProvider.GetRequiredService<TrackingStateLoader>();
        var member = ApplyMarks(await loader.LoadTrackedAsync(guildId, platform.Platform, current.Settings, userId, cancellationToken), userId);

        var tracked = new Dictionary<Guid, PlatformRef>(current.Tracked);
        tracked.Remove(userId);
        if (member.TryGetValue(userId, out var user))
        {
            tracked[userId] = user;
        }

        await ApplyAsync(
            scope.ServiceProvider,
            new TrackingSnapshot(guildId, current.Settings, current.TrackingRoleName, current.TrackingRole, tracked),
            userId,
            cancellationToken);
    }

    /// <remarks>Call under <c>_rebuild</c>.</remarks>
    private async Task RebuildAsync(Guid guildId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var loader = scope.ServiceProvider.GetRequiredService<TrackingStateLoader>();
        var (settings, roleName, role) = await loader.LoadSettingsAsync(guildId, platform.Platform, cancellationToken);
        var tracked = ApplyMarks(await loader.LoadTrackedAsync(guildId, platform.Platform, settings, userId: null, cancellationToken), userId: null);
        await ApplyAsync(scope.ServiceProvider, new TrackingSnapshot(guildId, settings, roleName, role, tracked), userId: null, cancellationToken);
    }

    /// <summary>
    /// Takes the marked users (only <paramref name="userId"/> when given) out of what the rule tracks, and ends the mark
    /// of every one the rule no longer tracks. Call under <c>_rebuild</c>.
    /// </summary>
    private Dictionary<Guid, PlatformRef> ApplyMarks(Dictionary<Guid, PlatformRef> tracked, Guid? userId)
    {
        foreach (var marked in _untracked.Where(id => userId == null || id == userId).ToList())
        {
            if (!tracked.Remove(marked))
            {
                _untracked.Remove(marked);
            }
        }

        return tracked;
    }

    /// <summary>
    /// Stores the periods, swaps in <paramref name="next"/>, logs a mode change and calls the hooks for every user whose
    /// tracking changed (only <paramref name="userId"/> when given).
    /// </summary>
    private async Task ApplyAsync(IServiceProvider services, TrackingSnapshot next, Guid? userId, CancellationToken cancellationToken)
    {
        var previous = _snapshot;
        var before = previous?.GuildId == next.GuildId ? previous.Tracked : _nobody;
        var stopped = before.Where(t => !next.Tracked.ContainsKey(t.Key)).ToList();
        var started = next.Tracked.Where(t => !before.ContainsKey(t.Key)).ToList();

        await services.GetRequiredService<TrackingStateLoader>()
            .SyncPeriodsAsync(next.GuildId, [.. next.Tracked.Keys], userId, timeProvider.GetUtcNow(), cancellationToken);

        _snapshot = next;
        if (previous is null || previous.Mode != next.Mode || previous.Settings.TrackingRoleId != next.Settings.TrackingRoleId)
        {
            LogMode(next);
        }

        foreach (var (id, user) in stopped)
        {
            await CallHooksAsync("TrackingStopped", new TrackedUser(next.GuildId, id, user), (h, u) => h.TrackingStoppedAsync(u, cancellationToken), cancellationToken);
        }

        foreach (var (id, user) in started)
        {
            await CallHooksAsync("TrackingStarted", new TrackedUser(next.GuildId, id, user), (h, u) => h.TrackingStartedAsync(u, cancellationToken), cancellationToken);
        }
    }

    /// <summary>
    /// Calls every handler for <paramref name="user"/>, each in a scope of its own, so a handler that leaves its scope
    /// broken (e.g. a <c>DbContext</c> after a failed save) affects neither the rebuild nor the next call. A failure is
    /// logged, including a handler's own cancellation; only the gate's <paramref name="cancellationToken"/> stops the
    /// remaining calls.
    /// </summary>
    private async Task CallHooksAsync(
        string hook,
        TrackedUser user,
        Func<ITrackingLifecycleHandler, TrackedUser, Task> call,
        CancellationToken cancellationToken)
    {
        int count;
        await using (var countScope = scopeFactory.CreateAsyncScope())
        {
            count = countScope.ServiceProvider.GetServices<ITrackingLifecycleHandler>().Count();
        }

        for (var index = 0; index < count; index++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices<ITrackingLifecycleHandler>().ElementAt(index);
            try
            {
                await call(handler, user);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                LogHookFailed(exception, hook, handler.GetType().Name, user.UserId);
            }
        }
    }

    private void LogMode(TrackingSnapshot snapshot)
    {
        switch (snapshot.Mode)
        {
            case TrackingMode.Bootstrap: // TEMPORARY: removed by #62
                LogBootstrapMode(platform.Platform);
                break;
            case TrackingMode.TrackingRole:
                LogTrackingRole(snapshot.TrackingRoleName, platform.Platform);
                break;
            case TrackingMode.TrackingRoleDeleted:
                LogTrackingRoleDeleted(snapshot.TrackingRoleName, platform.Platform);
                break;
            case TrackingMode.TrackingRoleCleared:
                LogTrackingRoleCleared(platform.Platform);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.Mode, "Unknown tracking mode.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bootstrap mode: tracking Administrators on {Platform} until a tracking role is set (temporary, #62)")]
    private partial void LogBootstrapMode(PlatformKind platform);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tracking role {RoleName} on {Platform}: tracking its holders")]
    private partial void LogTrackingRole(string? roleName, PlatformKind platform);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tracking role {RoleName} on {Platform} was deleted: nobody is tracked until a new tracking role is set")]
    private partial void LogTrackingRoleDeleted(string? roleName, PlatformKind platform);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tracking role on {Platform} was removed: nobody is tracked, Administrators included, until a tracking role is set")]
    private partial void LogTrackingRoleCleared(PlatformKind platform);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Hook} hook {Handler} failed for user {UserId}")]
    private partial void LogHookFailed(Exception exception, string hook, string handler, Guid userId);
}
