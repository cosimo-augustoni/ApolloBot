using Apollo.Platform.Abstractions.Events;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps VOICE_STATE_UPDATE to <see cref="PlatformVoiceStateChanged"/> (#16): the user's new channel (none when they
/// left) and their self mute/deaf, stream and video flags. Discord sends no timestamp, so the event is stamped on
/// receipt. The previous channel is not needed: the voice recorder keeps it in memory.
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordVoiceStateHandler(DiscordEventMapper mapper, TimeProvider timeProvider) : IVoiceStateUpdateGatewayHandler
{
    public ValueTask HandleAsync(VoiceState arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.VoiceStateUpdate), arg.GuildId, guild =>
            new PlatformVoiceStateChanged(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToVoiceState(arg)), CancellationToken.None);
}
