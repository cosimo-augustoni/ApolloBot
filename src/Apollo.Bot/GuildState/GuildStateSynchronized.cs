namespace Apollo.Bot.GuildState;

/// <summary>
/// The full reconciliation against a guild snapshot finished (on startup and after every reconnect). Published after
/// the <see cref="MemberAccessStateChanged"/> notifications of that run, also when nothing changed.
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
public sealed record GuildStateSynchronized(Guid GuildId) : GuildStateNotification(GuildId);
