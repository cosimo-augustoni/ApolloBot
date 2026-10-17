using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Tracking;

/// <summary>
/// The immutable state <see cref="TrackingGate"/> answers from: the role setting and every tracked user of one guild.
/// </summary>
internal sealed class TrackingSnapshot(
    Guid guildId,
    TrackingSettings settings,
    string? trackingRoleName,
    PlatformRef? trackingRole,
    IReadOnlyDictionary<Guid, PlatformRef> tracked)
{
    public Guid GuildId { get; } = guildId;

    public TrackingSettings Settings { get; } = settings;

    public TrackingMode Mode { get; } = TrackingRule.ModeOf(settings);

    /// <summary>
    /// The tracking role's name, for the log; <see langword="null"/> without one.
    /// </summary>
    public string? TrackingRoleName { get; } = trackingRoleName;

    /// <summary>
    /// The tracking role on the bot's platform while <see cref="Mode"/> is <see cref="TrackingMode.TrackingRole"/>;
    /// <see langword="null"/> otherwise.
    /// </summary>
    public PlatformRef? TrackingRole { get; } = TrackingRule.ModeOf(settings) == TrackingMode.TrackingRole ? trackingRole : null;

    /// <summary>
    /// The tracked users: internal ID to platform user.
    /// </summary>
    public IReadOnlyDictionary<Guid, PlatformRef> Tracked { get; } = tracked;

    /// <summary>
    /// The tracked users by platform user, for <see cref="ITrackingGate.TryGetTrackedUserId"/>.
    /// </summary>
    public IReadOnlyDictionary<PlatformRef, Guid> ByUser { get; } = tracked.ToDictionary(t => t.Value, t => t.Key);
}
