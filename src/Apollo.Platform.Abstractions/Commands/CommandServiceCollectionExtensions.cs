using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Platform.Abstractions.Commands;

public static class CommandServiceCollectionExtensions
{
    /// <summary>
    /// Adds a command the adapter registers on its platform. Definitions with the same name are merged
    /// (<see cref="CommandDefinition.Combine"/>), so each feature can add its own part of <c>apollo</c>.
    /// </summary>
    public static IServiceCollection AddCommandDefinition(this IServiceCollection services, CommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        services.AddSingleton(definition);
        return services;
    }
}
