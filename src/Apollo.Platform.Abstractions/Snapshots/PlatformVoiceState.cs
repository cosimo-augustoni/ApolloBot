namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// One voice connection of a member.
/// </summary>
/// <param name="User">The member.</param>
/// <param name="Channel">The voice channel; <see langword="null"/> when the connection left voice.</param>
/// <param name="ConnectionId">
/// Distinguishes several connections of one user. Fluxer reports voice states per connection; Discord has one per user
/// and leaves this <see langword="null"/>.
/// </param>
/// <param name="SelfMute">Muted by the user.</param>
/// <param name="SelfDeaf">Deafened by the user.</param>
/// <param name="Streaming">Sharing the screen ("Go Live").</param>
/// <param name="Video">Camera on.</param>
public sealed record PlatformVoiceState(
    PlatformRef User,
    PlatformRef? Channel,
    string? ConnectionId,
    bool SelfMute,
    bool SelfDeaf,
    bool Streaming,
    bool Video);
