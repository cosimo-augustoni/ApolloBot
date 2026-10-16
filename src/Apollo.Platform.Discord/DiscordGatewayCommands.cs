using NetCord.Gateway;

namespace Apollo.Platform.Discord;

internal sealed class DiscordGatewayCommands(GatewayClient gateway) : IDiscordGatewayCommands
{
    public ValueTask RequestGuildUsersAsync(GuildUsersRequestProperties request, CancellationToken cancellationToken) =>
        gateway.RequestGuildUsersAsync(request, cancellationToken: cancellationToken);
}
