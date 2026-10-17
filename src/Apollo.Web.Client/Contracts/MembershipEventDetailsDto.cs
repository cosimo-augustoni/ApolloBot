namespace Apollo.Web.Client.Contracts;

/// <summary>A <c>membership_events</c> row (scope §5.1, #18).</summary>
/// <param name="Kind">As stored: <c>Join</c>, <c>Leave</c>, <c>Ban</c> or <c>Unban</c>.</param>
public sealed record MembershipEventDetailsDto(string Kind);
