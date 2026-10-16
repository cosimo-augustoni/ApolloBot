using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Commands;

/// <summary>
/// Checks the command definitions and handlers when the host starts, before any hosted service (the gateway
/// included) runs, so a wiring mistake stops the bot instead of surfacing as error replies or a failed registration.
/// </summary>
/// <remarks>
/// Fails on duplicate definition paths, duplicate handler paths or button prefixes, and handlers whose path no
/// definition declares (the platform would never deliver it). A defined path without a handler only warns: members
/// get the generic error reply for it.
/// </remarks>
internal sealed partial class CommandCatalogValidator(
    IEnumerable<CommandDefinition> definitions,
    IServiceScopeFactory scopeFactory,
    ILogger<CommandCatalogValidator> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var definedPaths = CommandDefinition.Combine(definitions).SelectMany(definition => definition.Paths).ToHashSet(StringComparer.Ordinal);

        await using var scope = scopeFactory.CreateAsyncScope();
        var handlerPaths = scope.ServiceProvider.GetServices<ICommandHandler>().Select(handler => handler.Path).ToList();
        var buttonPrefixes = scope.ServiceProvider.GetServices<IButtonHandler>().Select(handler => handler.IdPrefix).ToList();

        ThrowOnDuplicate(handlerPaths, "Several command handlers declare the path");
        ThrowOnDuplicate(buttonPrefixes, "Several button handlers declare the prefix");

        var undefined = handlerPaths.Where(path => !definedPaths.Contains(path)).ToList();
        if (undefined.Count > 0)
        {
            throw new InvalidOperationException(
                $"No command definition declares the handler path(s) {string.Join(", ", undefined)}. Add them with AddCommandDefinition.");
        }

        foreach (var path in definedPaths.Except(handlerPaths, StringComparer.Ordinal))
        {
            LogPathWithoutHandler(path);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static void ThrowOnDuplicate(IEnumerable<string> values, string message)
    {
        var duplicate = values.GroupBy(value => value, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"{message} {duplicate.Key}.");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The command {CommandPath} is defined but has no handler; members get the error reply")]
    private partial void LogPathWithoutHandler(string commandPath);
}
