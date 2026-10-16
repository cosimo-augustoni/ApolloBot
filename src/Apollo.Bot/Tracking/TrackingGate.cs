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
    private volatile TrackingSnapshot? _snapshot;

    public TrackingMode? Mode => _snapshot?.Mode;

    public bool IsTracked(Guid userId) => _snapshot?.Tracked.ContainsKey(userId) == true;

    public Guid? TryGetTrackedUserId(PlatformRef user) =>
        _snapshot is { } snapshot && snapshot.ByUser.TryGetValue(user, out var userId) ? userId : null;

    /// <summary>
    /// Rebuilds the snapshot of the guild the gate already knows; does nothing before the first guild sync.
    /// </summary>
    public Task ReloadAsync(CancellationToken cancellationToken) =>
        _snapshot is { } snapshot ? ReloadAsync(snapshot.GuildId, cancellationToken) : Task.CompletedTask;

    /// <summary>
    /// Rebuilds the snapshot from the role setting and every current member of <paramref name="guildId"/>.
    /// </summary>
    public async Task ReloadAsync(Guid guildId, CancellationToken cancellationToken)
    {
        await _rebuild.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var loader = scope.ServiceProvider.GetRequiredService<TrackingStateLoader>();
            var (settings, roleName) = await loader.LoadSettingsAsync(guildId, platform.Platform, cancellationToken);
            var tracked = await loader.LoadTrackedAsync(guildId, platform.Platform, settings, userId: null, cancellationToken);
            await ApplyAsync(scope.ServiceProvider, new TrackingSnapshot(guildId, settings, roleName, tracked), userId: null, cancellationToken);
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
            if (_snapshot is not { } current || current.GuildId != guildId)
            {
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var loader = scope.ServiceProvider.GetRequiredService<TrackingStateLoader>();
            var member = await loader.LoadTrackedAsync(guildId, platform.Platform, current.Settings, userId, cancellationToken);

            var tracked = new Dictionary<Guid, PlatformRef>(current.Tracked);
            tracked.Remove(userId);
            if (member.TryGetValue(userId, out var user))
            {
                tracked[userId] = user;
            }

            await ApplyAsync(
                scope.ServiceProvider,
                new TrackingSnapshot(guildId, current.Settings, current.TrackingRoleName, tracked),
                userId,
                cancellationToken);
        }
        finally
        {
            _rebuild.Release();
        }
    }

    public void Dispose() => _rebuild.Dispose();

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

        var handlers = services.GetServices<ITrackingLifecycleHandler>().ToList();
        foreach (var (id, user) in stopped)
        {
            await CallHooksAsync(handlers, "TrackingStopped", new TrackedUser(next.GuildId, id, user), (h, u) => h.TrackingStoppedAsync(u, cancellationToken));
        }

        foreach (var (id, user) in started)
        {
            await CallHooksAsync(handlers, "TrackingStarted", new TrackedUser(next.GuildId, id, user), (h, u) => h.TrackingStartedAsync(u, cancellationToken));
        }
    }

    private async Task CallHooksAsync(
        IReadOnlyList<ITrackingLifecycleHandler> handlers,
        string hook,
        TrackedUser user,
        Func<ITrackingLifecycleHandler, TrackedUser, Task> call)
    {
        foreach (var handler in handlers)
        {
            try
            {
                await call(handler, user);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
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
