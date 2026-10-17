using Apollo.Bot.Resources;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Localization;

namespace Apollo.Bot.Privacy;

/// <summary>
/// The confirm button of <c>/apollo privacy delete</c> (scope §5.4): deletes the member's data with the
/// <see cref="IUserDataEraser"/> and says what happened. The router lets only the member the button was issued to through.
/// </summary>
internal sealed class PrivacyDeleteButtonHandler(IUserDataEraser eraser, IStringLocalizer<Strings> strings) : IButtonHandler
{
    public const string Prefix = "privacy-delete";

    public string IdPrefix => Prefix;

    public async Task<CommandReply> HandleAsync(ButtonInvocation invocation, CancellationToken cancellationToken)
    {
        var outcome = await eraser.EraseAsync(invocation.Guild, invocation.Member, cancellationToken);
        return CommandReply.Private(strings[outcome switch
        {
            UserDataErasureOutcome.DeletedAndRoleRemoved => "Privacy_DeletedAndRoleRemoved",
            UserDataErasureOutcome.Deleted => "Privacy_Deleted",
            UserDataErasureOutcome.DeletedButRoleKept => "Privacy_DeletedButRoleKept",
            UserDataErasureOutcome.DeletedNotSetUp => "Privacy_DeletedNotSetUp",
            UserDataErasureOutcome.DeletedInBootstrapMode => "Privacy_DeletedInBootstrapMode", // TEMPORARY: removed by #62
            UserDataErasureOutcome.Failed => "Privacy_DeleteFailed",
            _ => throw new ArgumentOutOfRangeException(nameof(invocation), outcome, "Unknown erasure outcome."),
        }]);
    }
}
