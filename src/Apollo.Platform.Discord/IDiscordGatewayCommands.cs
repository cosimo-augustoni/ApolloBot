using NetCord.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// The gateway commands the adapter sends. A seam over <see cref="GatewayClient"/>, which cannot send without a live
/// connection, so tests can answer the commands themselves.
/// </summary>
internal interface IDiscordGatewayCommands
{
    ValueTask RequestGuildUsersAsync(GuildUsersRequestProperties request, CancellationToken cancellationToken);
}
