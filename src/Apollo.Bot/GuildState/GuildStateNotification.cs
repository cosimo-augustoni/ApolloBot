namespace Apollo.Bot.GuildState;

/// <summary>
/// Base of the in-process notifications <see cref="GuildStateSync"/> publishes after it committed a change, for
/// consumers that cache guild state, such as the tracking gate (#14). Handled by
/// <see cref="IGuildStateNotificationHandler{TNotification}"/>.
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
public abstract record GuildStateNotification(Guid GuildId);
