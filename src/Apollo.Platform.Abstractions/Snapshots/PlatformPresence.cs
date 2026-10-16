namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// A member's presence.
/// </summary>
/// <param name="User">The member.</param>
/// <param name="Status">The online status.</param>
/// <param name="Activities">
/// The current activities; always empty on platforms without <see cref="PlatformCapabilities.PresenceActivities"/>.
/// </param>
public sealed record PlatformPresence(
    PlatformRef User,
    PlatformPresenceStatus Status,
    IReadOnlyList<PlatformActivity> Activities);
