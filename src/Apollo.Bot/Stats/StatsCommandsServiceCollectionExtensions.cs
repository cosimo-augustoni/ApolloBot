using Apollo.Bot.Commands;
using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Stats;

public static class StatsCommandsServiceCollectionExtensions
{
    /// <summary>
    /// The <c>apollo stats</c> part of the <c>apollo</c> command (#23, scope D5).
    /// </summary>
    public static readonly CommandDefinition Definition = new(ApolloCommand.Name, ApolloCommand.Description)
    {
        Subcommands = [new SubcommandDefinition("stats", "Link zu deinen Statistiken auf der Website")],
    };

    /// <summary>
    /// Registers <c>/apollo stats</c> (#23). Needs <see cref="CommandRoutingServiceCollectionExtensions.AddCommandRouting"/>.
    /// </summary>
    public static IServiceCollection AddStatsCommands(this IServiceCollection services)
    {
        services.AddCommandDefinition(Definition);
        services.AddCommandHandler<StatsCommandHandler>();

        return services;
    }
}
