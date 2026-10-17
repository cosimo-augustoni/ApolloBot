using Apollo.Bot.Commands;
using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Tracking;

public static class TrackingCommandsServiceCollectionExtensions
{
    /// <summary>
    /// The <c>apollo tracking</c> part of the <c>apollo</c> command (#21, scope §5.4).
    /// </summary>
    public static readonly CommandDefinition Definition = new(ApolloCommand.Name, ApolloCommand.Description)
    {
        Groups =
        [
            new SubcommandGroupDefinition(
                "tracking",
                "Erfassung deiner Aktivitäten",
                [
                    new SubcommandDefinition("on", "Erfassung deiner Aktivitäten einschalten"),
                    new SubcommandDefinition("off", "Erfassung deiner Aktivitäten ausschalten"),
                    new SubcommandDefinition("status", "Zeigt, ob und seit wann du erfasst wirst und welche Daten gespeichert sind"),
                ]),
        ],
    };

    /// <summary>
    /// Registers <c>/apollo tracking on|off|status</c> (#21). Needs
    /// <see cref="CommandRoutingServiceCollectionExtensions.AddCommandRouting"/> and
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/>.
    /// </summary>
    public static IServiceCollection AddTrackingCommands(this IServiceCollection services)
    {
        services.AddCommandDefinition(Definition);
        services.AddScoped<TrackingRoleSwitch>();
        services.AddCommandHandler<TrackingOnCommandHandler>();
        services.AddCommandHandler<TrackingOffCommandHandler>();
        services.AddScoped<IUserDataSummary, UserDataSummary>();
        services.AddCommandHandler<TrackingStatusCommandHandler>();

        return services;
    }
}
