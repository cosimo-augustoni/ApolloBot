namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// Answers the buttons whose <see cref="ButtonId.Prefix"/> is <see cref="IdPrefix"/>. The command router runs it only
/// for the member the button was issued to.
/// </summary>
public interface IButtonHandler
{
    /// <summary>
    /// The prefix of the button ids this handler answers, e.g. <c>privacy-delete</c>.
    /// </summary>
    string IdPrefix { get; }

    Task<CommandReply> HandleAsync(ButtonInvocation invocation, CancellationToken cancellationToken);
}
