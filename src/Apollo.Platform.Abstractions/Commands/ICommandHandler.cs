namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// Answers one command path. Handlers are scoped (one DI scope per invocation) and found by the command router.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// The command this handler answers, e.g. <c>apollo tracking status</c> (see <see cref="CommandInvocation.Path"/>).
    /// </summary>
    string Path { get; }

    Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken);
}
