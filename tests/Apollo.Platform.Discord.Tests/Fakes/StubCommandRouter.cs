using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// Stands in for the bot's command router: records what the adapter delivers and answers with <see cref="Reply"/>,
/// or waits for <see cref="Release"/> when <see cref="Hold"/> is set (a slow handler). With <see cref="Failure"/> set
/// it throws instead of answering, as a broken router would.
/// </summary>
internal sealed class StubCommandRouter : ICommandRouter
{
    public const string ErrorText = "Fehler (Test)";

    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _called = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CommandReply Reply { get; set; } = CommandReply.Private("Antwort");

    public bool Hold { get; set; }

    public Exception? Failure { get; set; }

    public List<CommandInvocation> Commands { get; } = [];

    public List<ButtonInvocation> Buttons { get; } = [];

    /// <summary>
    /// Completes once the adapter called the router.
    /// </summary>
    public Task Called => _called.Task;

    public void Release() => _release.TrySetResult();

    public Task<CommandReply> HandleCommandAsync(CommandInvocation invocation, CancellationToken cancellationToken)
    {
        Commands.Add(invocation);
        return AnswerAsync();
    }

    public Task<CommandReply> HandleButtonAsync(ButtonInvocation invocation, CancellationToken cancellationToken)
    {
        Buttons.Add(invocation);
        return AnswerAsync();
    }

    public CommandReply CreateErrorReply() => CommandReply.Private(ErrorText);

    private async Task<CommandReply> AnswerAsync()
    {
        _called.TrySetResult();
        if (Hold)
        {
            await _release.Task;
        }

        return Failure is null ? Reply : throw Failure;
    }
}
