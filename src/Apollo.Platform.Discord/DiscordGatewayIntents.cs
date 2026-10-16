using NetCord.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// The gateway intents the recorders need (#12). Message content, presences and members are privileged and must also
/// be enabled in the Discord developer portal (scope §5.1).
/// </summary>
internal static class DiscordGatewayIntents
{
    public const GatewayIntents Required =
        GatewayIntents.Guilds
        | GatewayIntents.GuildMessages
        | GatewayIntents.MessageContent
        | GatewayIntents.GuildMessageReactions
        | GatewayIntents.GuildVoiceStates
        | GatewayIntents.GuildPresences
        | GatewayIntents.GuildUsers
        | GatewayIntents.GuildModeration;
}
