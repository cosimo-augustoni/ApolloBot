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
    /// with its single-guild filter and <see cref="IChatPlatform"/>.
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
        services.AddGatewayHandler<DiscordConnectionHandler>(ServiceLifetime.Singleton);
        services.AddSingleton<IChatPlatform, DiscordChatPlatform>();

        return services;
    }
}
