using Apollo.Platform.Abstractions.Dispatch;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Apollo.Bot.Status;

public static class BotStatusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the heartbeat (<see cref="BotHeartbeatService"/>, one <c>bot_status</c> row every 30 s), its
    /// <see cref="IBotStatusSource"/>, the gateway health check and <see cref="HealthStatusLogPublisher"/>, which runs it
    /// and logs its transitions. Needs an <c>IPlatformConnectionStatus</c> from the platform adapter and the
    /// <c>ApolloDbContext</c>, and an <see cref="IOpenVoiceSessionCounter"/> (the voice recorder's,
    /// <see cref="Voice.VoiceRecordingServiceCollectionExtensions.AddVoiceRecording"/>).
    /// </summary>
    public static IServiceCollection AddBotStatus(this IServiceCollection services)
    {
        services.AddPlatformEventDispatcher();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IBotStatusSource, BotStatusSource>();
        services.AddHostedService<BotHeartbeatService>();
        services.AddHealthChecks().AddCheck<GatewayConnectionHealthCheck>(GatewayConnectionHealthCheck.Name);
        services.AddSingleton<IHealthCheckPublisher, HealthStatusLogPublisher>();

        return services;
    }
}
