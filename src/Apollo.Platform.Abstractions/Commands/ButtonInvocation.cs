namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// A member used a <see cref="ReplyButton"/>. On Discord that is a button click; platforms without buttons (Fluxer)
/// deliver it from a confirm text command instead (docs/platform-parity.md, Command model).
/// </summary>
/// <param name="Guild">The guild the button was used in.</param>
/// <param name="Member">The member who used it.</param>
/// <param name="ButtonId">The <see cref="ReplyButton.Id"/>, built with <see cref="Commands.ButtonId"/>.</param>
/// <param name="At">When the member used it.</param>
public sealed record ButtonInvocation(PlatformRef Guild, PlatformRef Member, string ButtonId, DateTimeOffset At);
