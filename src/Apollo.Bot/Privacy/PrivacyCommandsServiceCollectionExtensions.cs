using Apollo.Bot.Commands;
using Apollo.Bot.Tracking;
using Apollo.Data.Activity;
using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Privacy;

public static class PrivacyCommandsServiceCollectionExtensions
{
    /// <summary>
    /// The <c>apollo privacy</c> part of the <c>apollo</c> command (#22, scope §5.4).
    /// </summary>
    public static readonly CommandDefinition Definition = new(ApolloCommand.Name, ApolloCommand.Description)
    {
        Groups =
        [
            new SubcommandGroupDefinition(
                "privacy",
                "Deine gespeicherten Daten",
                [new SubcommandDefinition("delete", "Alle über dich erfassten Daten endgültig löschen")]),
        ],
    };

    /// <summary>
    /// Registers <c>/apollo privacy delete</c> with its confirm button and the <see cref="IUserDataEraser"/> (#22). Needs
    /// <see cref="CommandRoutingServiceCollectionExtensions.AddCommandRouting"/> and
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/>.
    /// </summary>
    public static IServiceCollection AddPrivacyCommands(this IServiceCollection services)
    {
        services.AddCommandDefinition(Definition);
        services.AddCommandHandler<PrivacyDeleteCommandHandler>();
        services.AddButtonHandler<PrivacyDeleteButtonHandler>();
        services.AddScoped<IUserDataEraser, UserDataEraser>();
        services.AddScoped<IAggregateRefresher, ContinuousAggregateRefresher>();
        services.AddUserDataEraserStep<RecordedDataEraserStep>();

        return services;
    }

    /// <summary>
    /// Registers a scoped <see cref="IUserDataEraserStep"/>. Steps run in registration order, in the deletion's
    /// transaction.
    /// </summary>
    public static IServiceCollection AddUserDataEraserStep<TStep>(this IServiceCollection services)
        where TStep : class, IUserDataEraserStep
    {
        services.AddScoped<IUserDataEraserStep, TStep>();
        return services;
    }
}
