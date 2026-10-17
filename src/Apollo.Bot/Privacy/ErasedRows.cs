namespace Apollo.Bot.Privacy;

/// <summary>
/// What a data deletion did to one table, for its log.
/// </summary>
/// <param name="Table">The table name, e.g. <c>message_events</c>.</param>
/// <param name="Deleted">The user's own rows deleted.</param>
/// <param name="Cleared">Other users' rows whose reference to the user was set to <c>NULL</c>.</param>
public sealed record ErasedRows(string Table, int Deleted, int Cleared = 0);
