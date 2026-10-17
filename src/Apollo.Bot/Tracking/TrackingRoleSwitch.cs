using Apollo.Bot.Resources;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.Localization;

namespace Apollo.Bot.Tracking;

/// <summary>
/// Gives or takes the invoking member's tracking role for <c>/apollo tracking on|off</c> (scope §5.4, D15). Only the
/// role changes here: the platform echoes the member update, the guild state sync stores it and the gate starts or
/// stops tracking (#13, #14), exactly as for a role change by hand. Nothing changes while the gate has no tracking role
/// (bootstrap, a deleted or cleared role, or before the first guild sync).
/// </summary>
internal sealed partial class TrackingRoleSwitch(
    ITrackingGate gate,
    IChatPlatform platform,
    IStringLocalizer<Strings> strings,
    ILogger<TrackingRoleSwitch> logger)
{
    /// <param name="invocation">The command; only its member's role changes.</param>
    /// <param name="on"><see langword="true"/> to give the role, <see langword="false"/> to take it.</param>
    /// <param name="cancellationToken">Cancels the platform calls.</param>
    public async Task<CommandReply> SwitchAsync(CommandInvocation invocation, bool on, CancellationToken cancellationToken)
    {
        // One view for the whole command, so the role and whether the member holds it come from the same snapshot.
        var view = gate.Current;
        if (view.TrackingRole is not { } role)
        {
            return Reply("Tracking_NotSetUp");
        }

        // With a tracking role, the gate tracks exactly its holders. Its snapshot comes from the full member sync, while
        // the platform's member reads may come from an incomplete gateway cache.
        var holdsRole = view.TryGetTrackedUserId(invocation.Member) is not null;
        if (holdsRole == on)
        {
            return Reply(on ? "Tracking_AlreadyOn" : "Tracking_AlreadyOff");
        }

        try
        {
            if (on)
            {
                await platform.AddRoleAsync(invocation.Guild, invocation.Member, role, cancellationToken);
            }
            else
            {
                await platform.RemoveRoleAsync(invocation.Guild, invocation.Member, role, cancellationToken);
            }
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            if (on)
            {
                LogAddFailed(exception, role.ToString(), invocation.Member.ToString());
            }
            else
            {
                LogRemoveFailed(exception, role.ToString(), invocation.Member.ToString());
            }

            return Reply("Tracking_ChangeFailed");
        }

        return Reply(on ? "Tracking_On" : "Tracking_Off");
    }

    private CommandReply Reply(string name) => CommandReply.Private(strings[name]);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not add the tracking role {Role} to {Member}")]
    private partial void LogAddFailed(Exception exception, string role, string member);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not remove the tracking role {Role} from {Member}")]
    private partial void LogRemoveFailed(Exception exception, string role, string member);
}
