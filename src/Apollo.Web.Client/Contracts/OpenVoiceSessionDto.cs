namespace Apollo.Web.Client.Contracts;

/// <summary>A voice session of <see cref="BotStatusDto.OpenVoiceSessions"/>: a tracked member in a voice channel right now.</summary>
/// <param name="MemberName">The member's synced display name; <see langword="null"/> before the guild state sync named them.</param>
/// <param name="ChannelId">The internal ID of the channel, so channels without a synced name still count apart.</param>
/// <param name="ChannelName">The channel's synced name; <see langword="null"/> before the guild state sync named it.</param>
/// <param name="Since">When the session started (UTC).</param>
public sealed record OpenVoiceSessionDto(string? MemberName, Guid ChannelId, string? ChannelName, DateTimeOffset Since);
