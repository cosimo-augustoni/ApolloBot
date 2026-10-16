namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// The entry point of the adapters for commands and buttons: finds the handler and returns the reply to deliver. It
/// does not throw for a missing or failing handler; it answers with a generic error reply instead.
/// </summary>
/// <remarks>
/// Adapters call it only for the configured guild (scope §4.3), the same way they filter platform events.
/// </remarks>
public interface ICommandRouter
{
    Task<CommandReply> HandleCommandAsync(CommandInvocation invocation, CancellationToken cancellationToken);

    Task<CommandReply> HandleButtonAsync(ButtonInvocation invocation, CancellationToken cancellationToken);
}
