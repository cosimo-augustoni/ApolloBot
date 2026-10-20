namespace Apollo.Domain.Names;

/// <summary>
/// A stored row of a member's name history: their names and when the row was stamped.
/// </summary>
public sealed record RecordedMemberNames(MemberNames Names, DateTimeOffset At);
