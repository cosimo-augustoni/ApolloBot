using Apollo.Platform.Abstractions.Events;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps GUILD_MEMBER_ADD / UPDATE / REMOVE to the neutral member events. Add and update carry the full member with
/// its roles.
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordMemberHandler(GatewayClient gateway, DiscordEventMapper mapper, TimeProvider timeProvider)
    : IGuildUserAddGatewayHandler, IGuildUserUpdateGatewayHandler, IGuildUserRemoveGatewayHandler
{
    ValueTask IGuildUserAddGatewayHandler.HandleAsync(GuildUser arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildUserAdd), arg.GuildId, guild =>
            new PlatformMemberJoined(guild, timeProvider.GetUtcNow(), ToMember(arg)), CancellationToken.None);

    ValueTask IGuildUserUpdateGatewayHandler.HandleAsync(GuildUser arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildUserUpdate), arg.GuildId, guild =>
            new PlatformMemberUpdated(guild, timeProvider.GetUtcNow(), ToMember(arg)), CancellationToken.None);

    public ValueTask HandleAsync(GuildUserRemoveEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.GuildUserRemove), arg.GuildId, guild =>
            new PlatformMemberLeft(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(arg.User.Id)), CancellationToken.None);

    // The member's permissions come from the guild's roles, which the cache holds since GUILD_CREATE.
    private Abstractions.Snapshots.PlatformMember ToMember(GuildUser user) =>
        DiscordSnapshots.ToMember(user, gateway.Cache.Guilds.GetValueOrDefault(user.GuildId));
}
