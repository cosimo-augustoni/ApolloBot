namespace Apollo.Web.Client.Contracts;

/// <summary>
/// A member's stats from the raw event tables (#36) over a range, with names resolved on the server. Every list is
/// ordered most first.
/// </summary>
/// <param name="TopEmojis">The emojis they reacted with most.</param>
/// <param name="VoiceBuddies">Whom they spent most time with in the same voice channel.</param>
/// <param name="RepliesTo">Whom they replied to most.</param>
/// <param name="RepliedBy">Who replied to them most.</param>
/// <param name="LongestVoiceSession">Their longest closed voice session that started in the range; <see langword="null"/> without one.</param>
/// <param name="TopGames">The games they played most; empty without presence data (a bonus section).</param>
public sealed record MemberHighlightsDto(
    IReadOnlyList<EmojiUsageDto> TopEmojis,
    IReadOnlyList<VoiceBuddyDto> VoiceBuddies,
    IReadOnlyList<ReplyPartnerDto> RepliesTo,
    IReadOnlyList<ReplyPartnerDto> RepliedBy,
    LongestVoiceSessionDto? LongestVoiceSession,
    IReadOnlyList<GamePlaytimeDto> TopGames)
{
    /// <summary>No highlights at all.</summary>
    public static MemberHighlightsDto Empty { get; } = new([], [], [], [], null, []);
}
