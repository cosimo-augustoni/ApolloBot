namespace Apollo.Bot.Reconciliation;

public static class ReconciliationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ReconciliationOptions"/> (validated on start) for the periodic voice and presence runs. Safe
    /// to call more than once.
    /// </summary>
    public static IServiceCollection AddReconciliationOptions(this IServiceCollection services)
    {
        if (services.Any(d => d.ServiceType == typeof(ReconciliationOptionsMarker)))
        {
            return services;
        }

        services.AddSingleton<ReconciliationOptionsMarker>();
        services.AddOptions<ReconciliationOptions>()
            .BindConfiguration(ReconciliationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    private sealed class ReconciliationOptionsMarker;
}
