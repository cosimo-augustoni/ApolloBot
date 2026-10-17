using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Bot.Tracking;

/// <summary>
/// <c>/apollo tracking off</c> (scope §5.4): takes the tracking role from the member. Data already stored stays.
/// </summary>
internal sealed class TrackingOffCommandHandler(TrackingRoleSwitch roleSwitch) : ICommandHandler
{
    public string Path => "apollo tracking off";

    public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken) =>
        roleSwitch.SwitchAsync(invocation, on: false, cancellationToken);
}
