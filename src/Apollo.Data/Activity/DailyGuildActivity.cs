namespace Apollo.Data.Activity;

/// <summary>
/// One row of <c>daily_guild_activity</c> (scope §5.3, #34): a guild's activity on one Zurich day, split by
/// <see cref="IsBot"/> so stats can leave bots out. Read-only; a view over <c>daily_user_channel_activity</c>.
/// </summary>
public sealed class DailyGuildActivity
{
    /// <inheritdoc cref="DailyUserChannelActivity.Day"/>
    public DateTimeOffset Day { get; init; }

    public Guid GuildId { get; init; }

    /// <inheritdoc cref="DailyUserChannelActivity.IsBot"/>
    public bool IsBot { get; init; }

    /// <inheritdoc cref="DailyUserChannelActivity.MessageCount"/>
    public long MessageCount { get; init; }

    /// <inheritdoc cref="DailyUserChannelActivity.CharacterCount"/>
    public long CharacterCount { get; init; }

    /// <summary>
    /// Reactions added (<c>Add</c> rows).
    /// </summary>
    public long ReactionCount { get; init; }
}
