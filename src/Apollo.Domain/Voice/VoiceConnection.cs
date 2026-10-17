namespace Apollo.Domain.Voice;

/// <summary>
/// One voice connection of a user, with internal IDs.
/// </summary>
/// <param name="ConnectionId">
/// Distinguishes several connections of one user (Fluxer); <see langword="null"/> on Discord, which has one per user.
/// </param>
/// <param name="ChannelId">The internal voice channel ID; <see langword="null"/> when the connection left voice.</param>
/// <param name="Flags">The connection's mute/deaf/stream/video flags.</param>
public sealed record VoiceConnection(string? ConnectionId, Guid? ChannelId, VoiceFlags Flags);
