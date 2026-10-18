namespace Apollo.Data.Activity;

/// <summary>
/// One row of <c>daily_user_channel_activity</c> (scope §5.3, #34): what one user did in one channel or thread on one
/// Zurich day. Read-only; a view over the per-source continuous aggregates (migration <c>AddActivityAggregates</c>).
/// </summary>
public sealed class DailyUserChannelActivity
{
    /// <summary>
    /// The UTC instant of the Zurich midnight that starts the day (e.g. <c>2026-03-28T23:00Z</c> for 29 March). Turn it
    /// into a Zurich date with <see cref="TimeZoneInfo"/>; never cast it to <c>date</c> in SQL (ADR 0001).
    /// </summary>
    public DateTimeOffset Day { get; init; }

    public Guid GuildId { get; init; }

    public Guid UserId { get; init; }

    /// <summary>
    /// The channel or thread; a thread keeps its own ID (D2).
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// For a thread, its parent channel (from <c>channels</c>), so stats can roll threads up (D2).
    /// </summary>
    public Guid? ThreadParentId { get; init; }

    /// <summary>
    /// Whether the user is a bot: their messages say so, or <c>guild_members</c> does. Stats leave bots out by default
    /// (scope §5.1).
    /// </summary>
    public bool IsBot { get; init; }

    /// <summary>
    /// Messages sent: <c>Create</c> rows only, so edits do not count and a deleted message still counts once.
    /// </summary>
    public long MessageCount { get; init; }

    /// <summary>
    /// The sum of <c>length</c> over the <c>Create</c> rows.
    /// </summary>
    public long CharacterCount { get; init; }

    /// <summary>
    /// Reactions the user added (<c>Add</c> rows by the user).
    /// </summary>
    public long ReactionsGiven { get; init; }

    /// <summary>
    /// Reactions others added to the user's messages in the channel (<c>Add</c> rows whose <c>message_author_id</c> is
    /// the user).
    /// </summary>
    public long ReactionsReceived { get; init; }

    /// <summary>
    /// Seconds of the user's closed voice sessions in the channel. A session counts whole on the Zurich day it started.
    /// </summary>
    public long VoiceSeconds { get; init; }
}
