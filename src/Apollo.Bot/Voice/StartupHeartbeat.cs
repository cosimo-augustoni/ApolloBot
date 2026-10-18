namespace Apollo.Bot.Voice;

/// <summary>
/// The latest connected <c>bot_status</c> heartbeat of <paramref name="GuildId"/> before this run started, read once for
/// the startup reconciliation (#98).
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
/// <param name="At">When it was written; <see langword="null"/> when there is none.</param>
internal sealed record StartupHeartbeat(Guid GuildId, DateTimeOffset? At);
