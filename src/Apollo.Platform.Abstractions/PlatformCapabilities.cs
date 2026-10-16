namespace Apollo.Platform.Abstractions;

/// <summary>
/// What a platform can deliver, so the bot and the UI can tell "no data" from "not supported"
/// (docs/platform-parity.md, Consequences).
/// </summary>
/// <param name="PresenceActivities">
/// Whether presences carry activities (games, music, ...). Discord: yes. Fluxer: no, its presence has none.
/// </param>
public sealed record PlatformCapabilities(bool PresenceActivities);
