namespace Apollo.Web.Client.Contracts;

/// <summary>A row that names a user who was not tracked at its time (#40, scope §5.1.1).</summary>
/// <param name="Table">The table of the row, e.g. <c>message_events</c>.</param>
/// <param name="Column">The column that names the user, e.g. <c>user_id</c> or <c>reply_to_user_id</c>.</param>
/// <param name="Id">The row ID.</param>
/// <param name="User">The untracked user.</param>
/// <param name="OccurredAt">The time of the row, the start for sessions (UTC).</param>
public sealed record UntrackedRowDto(string Table, string Column, long Id, EventUserDto User, DateTimeOffset OccurredAt);
