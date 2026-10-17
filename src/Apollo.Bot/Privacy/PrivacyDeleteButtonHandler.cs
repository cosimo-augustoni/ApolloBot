using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Privacy;

internal sealed class PrivacyDeleteButtonHandler(IUserDataEraser eraser) : IButtonHandler
{
    public const string Prefix = "privacy-delete";

    public string IdPrefix => Prefix;

    public async Task<CommandReply> HandleAsync(ButtonInvocation invocation, CancellationToken cancellationToken)
    {
        await eraser.EraseAsync(invocation.Guild, invocation.Member, cancellationToken);
        return CommandReply.Private(string.Empty);
    }
}
