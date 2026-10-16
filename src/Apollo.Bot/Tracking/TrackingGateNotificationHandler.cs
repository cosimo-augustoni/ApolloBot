using Apollo.Bot.GuildState;

namespace Apollo.Bot.Tracking;

/// <summary>
/// Feeds the guild state sync's notifications (#13) into the <see cref="TrackingGate"/>: a full reload after every
/// snapshot (the first one is the startup reconciliation), a single-member re-evaluation after every member change.
/// </summary>
internal sealed class TrackingGateNotificationHandler(TrackingGate gate)
    : IGuildStateNotificationHandler<GuildStateSynchronized>, IGuildStateNotificationHandler<MemberAccessStateChanged>
{
    public Task HandleAsync(GuildStateSynchronized notification, CancellationToken cancellationToken) =>
        gate.ReloadAsync(notification.GuildId, cancellationToken);

    public Task HandleAsync(MemberAccessStateChanged notification, CancellationToken cancellationToken) =>
        gate.ReevaluateMemberAsync(notification.GuildId, notification.UserId, cancellationToken);
}
