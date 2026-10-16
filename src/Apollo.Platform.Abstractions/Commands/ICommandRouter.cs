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

    /// <summary>
    /// The generic error reply (private, with the privacy footer), for adapters that fail before or after routing, e.g.
    /// when an invocation cannot be mapped. The member always gets an answer.
    /// </summary>
    CommandReply CreateErrorReply();
}
