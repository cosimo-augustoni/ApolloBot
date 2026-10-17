using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

public static class DiscordPlatformServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Discord adapter: the NetCord gateway client (connected while the host runs), the event mapping
    /// with its single-guild filter (connection, guild snapshot, members, roles, channels and threads, messages, voice
    /// states, reactions), <see cref="IChatPlatform"/>, and the slash commands: the registered
    /// <see cref="Abstractions.Commands.CommandDefinition"/>s become guild commands on READY, and interactions go to the
    /// <see cref="Abstractions.Commands.ICommandRouter"/>, which the host must register (the bot's <c>AddCommandRouting</c>).
    /// </summary>
    public static IServiceCollection AddDiscordPlatform(this IServiceCollection services)
    {
        services.AddOptions<DiscordPlatformOptions>()
            .BindConfiguration(DiscordPlatformOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDiscordGateway((gateway, serviceProvider) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DiscordPlatformOptions>>().Value;
            gateway.Token = options.Token;
            gateway.Intents = DiscordGatewayIntents.Required;
            gateway.AutoStartStop = options.Connect;
        });

        services.AddPlatformEventDispatcher();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DiscordEventMapper>();
        services.AddSingleton<DiscordConnectionStatus>();
        services.AddSingleton<IPlatformConnectionStatus>(sp => sp.GetRequiredService<DiscordConnectionStatus>());
        services.AddGatewayHandler<DiscordConnectionHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordCommandRegistrar>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordInteractionHandler>(ServiceLifetime.Singleton);

        // Guild state sync (#13). The downloader is one instance: it sends the member requests and receives the chunks.
        services.AddSingleton<IDiscordGatewayCommands, DiscordGatewayCommands>();
        services.AddSingleton<DiscordMemberDownloader>();
        services.AddGatewayHandler(serviceProvider => serviceProvider.GetRequiredService<DiscordMemberDownloader>());
        services.AddGatewayHandler<DiscordGuildHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordMemberHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordBanHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordRoleHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordChannelHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordMessageHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordVoiceStateHandler>(ServiceLifetime.Singleton);
        services.AddGatewayHandler<DiscordReactionHandler>(ServiceLifetime.Singleton);

        services.AddSingleton<IChatPlatform, DiscordChatPlatform>();

        return services;
    }
}
