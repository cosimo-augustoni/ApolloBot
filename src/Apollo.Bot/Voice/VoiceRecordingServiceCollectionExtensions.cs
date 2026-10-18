using Apollo.Bot.GuildState;
using Apollo.Bot.Status;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Bot.Voice;

public static class VoiceRecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the voice recorder (#16): voice state updates, the gateway connection state, the tracking hooks, the
    /// reconciliation after every full guild sync, <see cref="VoiceSessionLifecycleService"/> for the startup take-over and
    /// the periodic reconciliation (#98), <see cref="VoiceReconciliationOptions"/> (validated on start) and the
    /// <see cref="IOpenVoiceSessionCounter"/> for the heartbeat. Needs
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/> registered first (so the gate handles a guild
    /// sync before the recorder reconciles), an <see cref="Apollo.Platform.Abstractions.IChatPlatform"/> and the
    /// <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddVoiceRecording(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<VoiceReconciliationOptions>()
            .BindConfiguration(VoiceReconciliationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<VoiceSessionStore>();
        services.AddSingleton<IOpenVoiceSessionCounter>(serviceProvider => serviceProvider.GetRequiredService<VoiceSessionStore>());
        services.AddScoped<VoiceSessionRecorder>();
        services.AddPlatformEventHandler<PlatformVoiceStateChanged, VoiceSessionRecorder>();
        services.AddPlatformEventHandler<PlatformConnectionStateChanged, VoiceSessionRecorder>();
        services.AddTrackingLifecycleHandler<VoiceSessionRecorder>();
        services.AddGuildStateNotificationHandler<GuildStateSynchronized, VoiceSessionRecorder>();
        services.AddHostedService<VoiceSessionLifecycleService>();

        return services;
    }
}
