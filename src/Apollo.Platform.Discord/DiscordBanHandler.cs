using Apollo.Platform.Abstractions.Events;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps GUILD_BAN_ADD / REMOVE to <see cref="PlatformMemberBanned"/> / <see cref="PlatformMemberUnbanned"/> (#18).
/// Discord sends no timestamp, so the events are stamped on receipt. A ban of a member also sends GUILD_MEMBER_REMOVE,
/// which <see cref="DiscordMemberHandler"/> maps to a separate <see cref="PlatformMemberLeft"/>.
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordBanHandler(DiscordEventMapper mapper, TimeProvider timeProvider)
    : IGuildBanAddGatewayHandler, IGuildBanRemoveGatewayHandler
{
    ValueTask IGuildBanAddGatewayHandler.HandleAsync(GuildBanEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildBanAdd), arg.GuildId, guild =>
            new PlatformMemberBanned(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(arg.User.Id)), CancellationToken.None);

    ValueTask IGuildBanRemoveGatewayHandler.HandleAsync(GuildBanEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildBanRemove), arg.GuildId, guild =>
            new PlatformMemberUnbanned(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(arg.User.Id)), CancellationToken.None);
}
