namespace Apollo.Web.Client.Contracts;

/// <summary>
/// One row of a raw event table (#32, scope §5.1): metadata only, never message content (it is never stored). Exactly
/// the details property of its <see cref="Type"/> is set.
/// </summary>
/// <param name="Id">The row's ID in its table.</param>
/// <param name="Type">The table.</param>
/// <param name="OccurredAt">When it happened (UTC); for sessions and segments their start.</param>
/// <param name="User">The tracked user the row belongs to.</param>
/// <param name="Channel">The channel or thread; <see langword="null"/> for tables without one.</param>
public sealed record RawEventDto(long Id, DataEventType Type, DateTimeOffset OccurredAt, EventUserDto User, EventChannelDto? Channel)
{
    public MessageEventDetailsDto? Message { get; init; }

    public VoiceSessionDetailsDto? VoiceSession { get; init; }

    public VoiceSegmentDetailsDto? VoiceSegment { get; init; }

    public ReactionEventDetailsDto? Reaction { get; init; }

    public MembershipEventDetailsDto? Membership { get; init; }

    public PresenceActivityDetailsDto? PresenceActivity { get; init; }

    public PresenceSegmentDetailsDto? PresenceSegment { get; init; }

    public MemberNameDetailsDto? MemberName { get; init; }
}
