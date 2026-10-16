using Apollo.Bot.Links;
using Apollo.Bot.Resources;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Apollo.Bot.Commands;

/// <summary>
/// Finds the <see cref="ICommandHandler"/> or <see cref="IButtonHandler"/> for an invocation and runs it in its own DI
/// scope. Missing and failing handlers are logged and answered with a generic German error; a button used by anyone
/// but its owner is turned away. Every reply gets the privacy page footer (scope §5.4 Transparency).
/// </summary>
internal sealed partial class CommandRouter(
    IServiceScopeFactory scopeFactory,
    IStringLocalizer<Strings> strings,
    IOptions<LinksOptions> links,
    ILogger<CommandRouter> logger) : ICommandRouter
{
    public async Task<CommandReply> HandleCommandAsync(CommandInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<ICommandHandler>()
            .FirstOrDefault(candidate => string.Equals(candidate.Path, invocation.Path, StringComparison.Ordinal));
        if (handler is null)
        {
            LogUnknownCommand(invocation.Path, invocation.Member.ToString());
            return WithFooter(Error());
        }

        try
        {
            return WithFooter(await handler.HandleAsync(invocation, cancellationToken));
        }
        catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
        {
            LogCommandFailed(exception, invocation.Path, handler.GetType().Name);
            return WithFooter(Error());
        }
    }

    public async Task<CommandReply> HandleButtonAsync(ButtonInvocation invocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!ButtonId.TryParse(invocation.ButtonId, out var buttonId))
        {
            LogUnknownButton(invocation.ButtonId, invocation.Member.ToString());
            return WithFooter(Error());
        }

        if (!buttonId.IsFor(invocation.Member))
        {
            LogForeignButton(invocation.ButtonId, invocation.Member.ToString());
            return WithFooter(CommandReply.Private(strings["Reply_NotForYou"]));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IButtonHandler>()
            .FirstOrDefault(candidate => string.Equals(candidate.IdPrefix, buttonId.Prefix, StringComparison.Ordinal));
        if (handler is null)
        {
            LogUnknownButton(invocation.ButtonId, invocation.Member.ToString());
            return WithFooter(Error());
        }

        try
        {
            return WithFooter(await handler.HandleAsync(invocation, cancellationToken));
        }
        catch (Exception exception) when (!IsCancellation(exception, cancellationToken))
        {
            LogButtonFailed(exception, invocation.ButtonId, handler.GetType().Name);
            return WithFooter(Error());
        }
    }

    private static bool IsCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private CommandReply Error() => CommandReply.Private(strings["Reply_Error"]);

    private CommandReply WithFooter(CommandReply reply) =>
        reply with { Text = $"{reply.Text}\n\n{strings["Reply_PrivacyFooter", links.Value.GetPrivacyPageUrl()]}" };

    [LoggerMessage(Level = LogLevel.Warning, Message = "No command handler for {CommandPath}, run by {Member}")]
    private partial void LogUnknownCommand(string commandPath, string member);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command handler {Handler} failed for {CommandPath}")]
    private partial void LogCommandFailed(Exception exception, string commandPath, string handler);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No button handler for {ButtonId}, used by {Member}")]
    private partial void LogUnknownButton(string buttonId, string member);

    [LoggerMessage(Level = LogLevel.Information, Message = "Button {ButtonId} was used by {Member}, who it was not issued to")]
    private partial void LogForeignButton(string buttonId, string member);

    [LoggerMessage(Level = LogLevel.Error, Message = "Button handler {Handler} failed for {ButtonId}")]
    private partial void LogButtonFailed(Exception exception, string buttonId, string handler);
}
