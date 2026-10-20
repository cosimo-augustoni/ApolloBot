using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>
/// The raw event tables of the admin data page (#32, scope §5.1). The JSON name is also the <c>type</c> query value of
/// <c>GET /api/admin/data/events</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataEventType>))]
public enum DataEventType
{
    /// <summary><c>message_events</c>.</summary>
    [JsonStringEnumMemberName("message")]
    Message,

    /// <summary><c>voice_sessions</c>, by their start.</summary>
    [JsonStringEnumMemberName("voiceSession")]
    VoiceSession,

    /// <summary><c>voice_state_segments</c>, by their start.</summary>
    [JsonStringEnumMemberName("voiceStateSegment")]
    VoiceStateSegment,

    /// <summary><c>reaction_events</c>.</summary>
    [JsonStringEnumMemberName("reaction")]
    Reaction,

    /// <summary><c>membership_events</c>.</summary>
    [JsonStringEnumMemberName("membership")]
    Membership,

    /// <summary><c>presence_activity_sessions</c>, by their start.</summary>
    [JsonStringEnumMemberName("presenceActivity")]
    PresenceActivity,

    /// <summary><c>presence_activity_segments</c>, by their start.</summary>
    [JsonStringEnumMemberName("presenceActivitySegment")]
    PresenceActivitySegment,
}
