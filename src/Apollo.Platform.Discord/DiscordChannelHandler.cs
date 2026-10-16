using Apollo.Platform.Abstractions.Events;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps channel and thread create / update / delete to the neutral channel events. Threads are channels with a
/// <see cref="Abstractions.Snapshots.PlatformChannel.ThreadParent"/> (scope §5.1).
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordChannelHandler(DiscordEventMapper mapper, TimeProvider timeProvider)
    : IGuildChannelCreateGatewayHandler, IGuildChannelUpdateGatewayHandler, IGuildChannelDeleteGatewayHandler,
        IGuildThreadCreateGatewayHandler, IGuildThreadUpdateGatewayHandler, IGuildThreadDeleteGatewayHandler
{
    ValueTask IGuildChannelCreateGatewayHandler.HandleAsync(IGuildChannel arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildChannelCreate), arg.GuildId, guild =>
            new PlatformChannelCreated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToChannel(arg)), CancellationToken.None);

    ValueTask IGuildChannelUpdateGatewayHandler.HandleAsync(IGuildChannel arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildChannelUpdate), arg.GuildId, guild =>
            new PlatformChannelUpdated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToChannel(arg)), CancellationToken.None);

    ValueTask IGuildChannelDeleteGatewayHandler.HandleAsync(IGuildChannel arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildChannelDelete), arg.GuildId, guild =>
            new PlatformChannelDeleted(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(arg.Id)), CancellationToken.None);

    public ValueTask HandleAsync(GuildThreadCreateEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildThreadCreate), arg.Thread.GuildId, guild =>
            new PlatformChannelCreated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToChannel(arg.Thread)), CancellationToken.None);

    public ValueTask HandleAsync(GuildThread arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildThreadUpdate), arg.GuildId, guild =>
            new PlatformChannelUpdated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToChannel(arg)), CancellationToken.None);

    public ValueTask HandleAsync(GuildThreadDeleteEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildThreadDelete), arg.GuildId, guild =>
            new PlatformChannelDeleted(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(arg.ThreadId)), CancellationToken.None);
}
