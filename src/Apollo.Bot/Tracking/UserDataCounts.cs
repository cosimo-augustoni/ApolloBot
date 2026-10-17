namespace Apollo.Bot.Tracking;

/// <summary>
/// A user's rows per event table (scope §5.1). The detail tables <c>message_mentions</c> and
/// <c>voice_state_segments</c> are not counted: their rows belong to a message or a voice session counted here.
/// </summary>
/// <param name="Messages"><c>message_events</c>: sent, edited and deleted messages.</param>
/// <param name="Reactions"><c>reaction_events</c>: added and removed reactions.</param>
/// <param name="VoiceSessions"><c>voice_sessions</c>.</param>
/// <param name="PresenceSessions"><c>presence_activity_sessions</c>.</param>
/// <param name="MembershipEvents"><c>membership_events</c>: joins, leaves, bans and unbans.</param>
public sealed record UserDataCounts(long Messages, long Reactions, long VoiceSessions, long PresenceSessions, long MembershipEvents)
{
    public static UserDataCounts None { get; } = new(0, 0, 0, 0, 0);

    public long Total => Messages + Reactions + VoiceSessions + PresenceSessions + MembershipEvents;
}
