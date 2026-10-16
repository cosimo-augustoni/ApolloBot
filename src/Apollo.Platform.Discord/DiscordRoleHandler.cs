using Apollo.Platform.Abstractions.Events;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps GUILD_ROLE_CREATE / UPDATE / DELETE to the neutral role events. @everyone is skipped, like everywhere else in
/// the adapter (see <see cref="Abstractions.Snapshots.PlatformMember.Roles"/>).
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordRoleHandler(DiscordEventMapper mapper, TimeProvider timeProvider)
    : IRoleCreateGatewayHandler, IRoleUpdateGatewayHandler, IRoleDeleteGatewayHandler
{
    ValueTask IRoleCreateGatewayHandler.HandleAsync(Role arg) =>
        DiscordSnapshots.IsEveryone(arg)
            ? default
            : mapper.ForwardAsync(nameof(GatewayClient.RoleCreate), arg.GuildId, guild =>
                new PlatformRoleCreated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToRole(arg)), CancellationToken.None);

    ValueTask IRoleUpdateGatewayHandler.HandleAsync(Role arg) =>
        DiscordSnapshots.IsEveryone(arg)
            ? default
            : mapper.ForwardAsync(nameof(GatewayClient.RoleUpdate), arg.GuildId, guild =>
                new PlatformRoleUpdated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToRole(arg)), CancellationToken.None);

    public ValueTask HandleAsync(RoleDeleteEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.RoleDelete), arg.GuildId, guild =>
            new PlatformRoleDeleted(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(arg.RoleId)), CancellationToken.None);
}
