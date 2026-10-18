namespace Apollo.Web.Client.Contracts;

/// <summary>One channel of <see cref="ActivityStatsDto.Channels"/>, with the activity of its threads rolled up into it (D2).</summary>
/// <param name="ChannelId">The internal channel ID.</param>
/// <param name="Name">The channel's last known name; <see langword="null"/> when never synced.</param>
/// <param name="IsDeleted">Whether the channel no longer exists on the platform.</param>
/// <param name="Messages">Messages sent in the channel and its threads.</param>
/// <param name="VoiceSeconds">Seconds of voice sessions in the channel.</param>
public sealed record ChannelActivityDto(Guid ChannelId, string? Name, bool IsDeleted, long Messages, long VoiceSeconds);
