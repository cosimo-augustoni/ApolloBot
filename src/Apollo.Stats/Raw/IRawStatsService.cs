namespace Apollo.Stats.Raw;

/// <summary>
/// The stats that don't fit daily sums, read from the raw event tables (scope §5.3, #36). Voice queries ignore open
/// sessions; sessions closed as <c>Unobserved</c> count as they are. With <see cref="RawStatsQuery.UserId"/>, every
/// answer only holds data involving that user.
/// </summary>
/// <remarks>Every method throws <see cref="ArgumentException"/> when <see cref="RawStatsQuery.From"/> is after
/// <see cref="RawStatsQuery.To"/>, and <see cref="ArgumentOutOfRangeException"/> when <see cref="RawStatsQuery.Top"/>
/// is less than 1.</remarks>
public interface IRawStatsService
{
    /// <summary>
    /// Pairs of users whose closed voice sessions overlapped in the same channel, by the overlap inside the range, most
    /// first. For a user, their buddies (<see cref="VoiceBuddy.UserId"/> is that user).
    /// </summary>
    Task<IReadOnlyList<VoiceBuddy>> GetVoiceBuddiesAsync(RawStatsQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Who replies to whom, from the <c>Create</c> rows with a replied-to user.
    /// </summary>
    Task<ReplyGraph> GetReplyGraphAsync(RawStatsQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The longest closed voice session that started in the range, or <see langword="null"/> without one.
    /// </summary>
    Task<LongestVoiceSession?> GetLongestVoiceSessionAsync(RawStatsQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The most used reaction emojis (added reactions), most first.
    /// </summary>
    Task<IReadOnlyList<EmojiUsage>> GetTopEmojisAsync(RawStatsQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The most played games by time inside the range, most first; empty without presence data (a bonus section,
    /// scope §5.1).
    /// </summary>
    Task<IReadOnlyList<GamePlaytime>> GetTopGamesAsync(RawStatsQuery query, CancellationToken cancellationToken);
}
