namespace Apollo.Stats.Sanity;

/// <summary>A voice or presence session a check reports.</summary>
/// <param name="Table">The table of the row (<c>voice_sessions</c> or <c>presence_activity_sessions</c>).</param>
/// <param name="Id">The row's ID.</param>
/// <param name="UserId">The internal user ID.</param>
/// <param name="StartedAt">Its start, <c>occurred_at</c> (UTC).</param>
/// <param name="EndedAt">Its end (UTC); <see langword="null"/> while open.</param>
public sealed record SanitySession(string Table, long Id, Guid UserId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
