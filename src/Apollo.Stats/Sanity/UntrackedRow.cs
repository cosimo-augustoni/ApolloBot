namespace Apollo.Stats.Sanity;

/// <summary>A row that names a user who had no tracking period at its time (scope §5.1.1).</summary>
/// <param name="Table">The table of the row, e.g. <c>message_events</c>.</param>
/// <param name="Column">The column that names the user, e.g. <c>user_id</c> or <c>reply_to_user_id</c>.</param>
/// <param name="Id">The row's ID.</param>
/// <param name="UserId">The internal ID of the untracked user.</param>
/// <param name="OccurredAt">The row's <c>occurred_at</c>, the start for sessions (UTC).</param>
public sealed record UntrackedRow(string Table, string Column, long Id, Guid UserId, DateTimeOffset OccurredAt);
