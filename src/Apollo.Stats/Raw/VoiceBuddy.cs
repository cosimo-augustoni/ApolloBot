namespace Apollo.Stats.Raw;

/// <summary>
/// Two users who were in the same voice channel at the same time, for <see cref="OverlapSeconds"/> inside the range.
/// For the guild each pair appears once, in no particular order of the two; for a user's query, <see cref="UserId"/> is
/// that user.
/// </summary>
public sealed record VoiceBuddy(Guid UserId, Guid BuddyId, long OverlapSeconds);
