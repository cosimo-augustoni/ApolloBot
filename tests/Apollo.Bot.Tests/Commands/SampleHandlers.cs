using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Tests.Commands;

/// <summary>
/// What the sample handlers received.
/// </summary>
internal sealed class HandlerCalls
{
    public List<CommandInvocation> Commands { get; } = [];

    public List<ButtonInvocation> Buttons { get; } = [];
}

internal sealed class TrackingStatusHandler(HandlerCalls calls) : ICommandHandler
{
    public const string Text = "Du wirst erfasst.";

    public string Path => "apollo tracking status";

    public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken)
    {
        calls.Commands.Add(invocation);
        ReplyButton confirm = new(ButtonId.For(ConfirmButtonHandler.Prefix, invocation.Member).ToString(), "Bestätigen", ButtonStyle.Danger);
        return Task.FromResult(new CommandReply(Text, [confirm], ReplyVisibility.Public));
    }
}

internal sealed class ThrowingCommandHandler : ICommandHandler
{
    public string Path => "apollo broken";

    public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The handler failed.");
}

internal sealed class ConfirmButtonHandler(HandlerCalls calls) : IButtonHandler
{
    public const string Prefix = "confirm";

    public const string Text = "Bestätigt.";

    public string IdPrefix => Prefix;

    public Task<CommandReply> HandleAsync(ButtonInvocation invocation, CancellationToken cancellationToken)
    {
        calls.Buttons.Add(invocation);
        return Task.FromResult(CommandReply.Private(Text));
    }
}

internal sealed class ThrowingButtonHandler : IButtonHandler
{
    public const string Prefix = "explode";

    public string IdPrefix => Prefix;

    public Task<CommandReply> HandleAsync(ButtonInvocation invocation, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The button handler failed.");
}
