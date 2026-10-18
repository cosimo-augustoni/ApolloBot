namespace Apollo.Web.Client.Contracts;

/// <summary>A voice or presence session the data sanity report flags (#40).</summary>
/// <param name="Table">The table of the row, <c>voice_sessions</c> or <c>presence_activity_sessions</c>.</param>
/// <param name="Id">The row ID.</param>
/// <param name="User">Whose session it is.</param>
/// <param name="StartedAt">Its start (UTC).</param>
/// <param name="EndedAt">Its end (UTC); <see langword="null"/> while open.</param>
public sealed record SanitySessionDto(string Table, long Id, EventUserDto User, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
