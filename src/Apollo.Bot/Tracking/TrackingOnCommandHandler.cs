using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Tracking;

/// <summary>
/// <c>/apollo tracking on</c> (scope §5.4): gives the member the tracking role, at any time (also after
/// <c>off</c> or a data deletion).
/// </summary>
internal sealed class TrackingOnCommandHandler(TrackingRoleSwitch roleSwitch) : ICommandHandler
{
    public string Path => "apollo tracking on";

    public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken) =>
        roleSwitch.SwitchAsync(invocation, on: true, cancellationToken);
}
