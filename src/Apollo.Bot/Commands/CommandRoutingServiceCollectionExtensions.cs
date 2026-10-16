using Apollo.Bot.Links;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Bot.Commands;

public static class CommandRoutingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="ICommandRouter"/> the adapter delivers commands and buttons to, its German reply text
    /// and <see cref="LinksOptions"/> (validated on start). Definitions and handlers are checked when the host starts
    /// (<see cref="CommandCatalogValidator"/>). Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddCommandRouting(this IServiceCollection services)
    {
        services.AddOptions<LinksOptions>()
            .BindConfiguration(LinksOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddLocalization();
        services.TryAddSingleton<ICommandRouter, CommandRouter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, CommandCatalogValidator>());

        return services;
    }

    /// <summary>
    /// Registers a scoped handler for the command <see cref="ICommandHandler.Path"/> it declares.
    /// </summary>
    public static IServiceCollection AddCommandHandler<THandler>(this IServiceCollection services)
        where THandler : class, ICommandHandler
    {
        services.AddScoped<ICommandHandler, THandler>();
        return services;
    }

    /// <summary>
    /// Registers a scoped handler for the buttons with the <see cref="IButtonHandler.IdPrefix"/> it declares.
    /// </summary>
    public static IServiceCollection AddButtonHandler<THandler>(this IServiceCollection services)
        where THandler : class, IButtonHandler
    {
        services.AddScoped<IButtonHandler, THandler>();
        return services;
    }
}
