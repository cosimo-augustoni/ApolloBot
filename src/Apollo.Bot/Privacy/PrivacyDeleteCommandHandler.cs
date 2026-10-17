using Apollo.Bot.Resources;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Localization;

namespace Apollo.Bot.Privacy;

/// <summary>
/// <c>/apollo privacy delete</c> (scope §5.4): warns that the deletion is permanent and offers the confirm button of
/// <see cref="PrivacyDeleteButtonHandler"/>. Deletes nothing itself.
/// </summary>
internal sealed class PrivacyDeleteCommandHandler(IStringLocalizer<Strings> strings) : ICommandHandler
{
    public string Path => "apollo privacy delete";

    public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken)
    {
        var confirm = new ReplyButton(
            ButtonId.For(PrivacyDeleteButtonHandler.Prefix, invocation.Member).ToString(),
            strings["Privacy_DeleteButton"],
            ButtonStyle.Danger);
        return Task.FromResult(new CommandReply(strings["Privacy_DeletePrompt"], [confirm]));
    }
}
