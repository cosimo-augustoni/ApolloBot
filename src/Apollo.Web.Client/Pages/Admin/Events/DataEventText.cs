using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Formatting;
using Microsoft.Extensions.Localization;

namespace Apollo.Web.Client.Pages.Admin.Events;

/// <summary>
/// The German text of the admin data page (#32) for tables and raw event rows. Stored values (<c>Create</c>,
/// <c>BotRestart</c>, …) map to resource keys; a value this client does not know yet is shown as stored.
/// </summary>
public static class DataEventText
{
    private const string Separator = " · ";

    /// <summary>One row of the table in German, e.g. "Nachricht".</summary>
    public static string TypeName(IStringLocalizer localizer, DataEventType type)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return type switch
        {
            DataEventType.Message => localizer["Data_Type_Message"],
            DataEventType.VoiceSession => localizer["Data_Type_VoiceSession"],
            DataEventType.VoiceStateSegment => localizer["Data_Type_VoiceStateSegment"],
            DataEventType.Reaction => localizer["Data_Type_Reaction"],
            DataEventType.Membership => localizer["Data_Type_Membership"],
            _ => localizer["Data_Type_PresenceActivity"],
        };
    }

    /// <summary>The table in German, e.g. "Nachrichten".</summary>
    public static string TableLabel(IStringLocalizer localizer, DataEventType type)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return type switch
        {
            DataEventType.Message => localizer["Data_Table_Message"],
            DataEventType.VoiceSession => localizer["Data_Table_VoiceSession"],
            DataEventType.VoiceStateSegment => localizer["Data_Table_VoiceStateSegment"],
            DataEventType.Reaction => localizer["Data_Table_Reaction"],
            DataEventType.Membership => localizer["Data_Table_Membership"],
            _ => localizer["Data_Table_PresenceActivity"],
        };
    }

    /// <summary>The SQL table, shown next to the label on purpose: the page is for debugging.</summary>
    public static string TableName(DataEventType type) => type switch
    {
        DataEventType.Message => "message_events",
        DataEventType.VoiceSession => "voice_sessions",
        DataEventType.VoiceStateSegment => "voice_state_segments",
        DataEventType.Reaction => "reaction_events",
        DataEventType.Membership => "membership_events",
        _ => "presence_activity_sessions",
    };

    /// <summary>Whether rows of <paramref name="type"/> have a channel, so the channel filter applies.</summary>
    public static bool HasChannel(DataEventType type) =>
        type is DataEventType.Message or DataEventType.VoiceSession or DataEventType.Reaction;

    /// <summary>The type of a row with its kind, e.g. "Nachricht · erstellt".</summary>
    public static string TypeLabel(IStringLocalizer localizer, RawEventDto row)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(row);
        return row switch
        {
            { Message: { } message } => localizer["Data_Type_Message"] + Separator + Kind(localizer, message.Kind),
            { Reaction: { } reaction } => localizer["Data_Type_Reaction"] + Separator + Kind(localizer, reaction.Kind),
            { Membership: { } membership } => localizer["Data_Type_Membership"] + Separator + Kind(localizer, membership.Kind),
            { PresenceActivity: { } activity } => localizer["Data_Type_PresenceActivity"] + Separator + Activity(localizer, activity.ActivityType),
            { VoiceSegment: not null } => localizer["Data_Type_VoiceStateSegment"],
            _ => localizer["Data_Type_VoiceSession"],
        };
    }

    /// <summary>The type-specific metadata of a row in one line; "–" when there is none.</summary>
    public static string Details(IStringLocalizer localizer, RawEventDto row)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(row);
        IEnumerable<string> parts = row switch
        {
            { Message: { } message } => MessageDetails(localizer, message),
            { Reaction: { } reaction } =>
            [
                reaction.IsCustomEmoji ? $":{reaction.EmojiName}:" : reaction.EmojiName,
                localizer["Data_ReactionOn", reaction.MessageAuthor is { } author ? UserName(localizer, author) : localizer["Data_NotTracked"]],
            ],
            { VoiceSession: { } session } => session.EndedAt is { } ended
                ? [Until(localizer, ended, row.OccurredAt), EndReason(localizer, session.EndReason)]
                : [localizer["Data_Open"]],
            { VoiceSegment: { } segment } =>
            [
                localizer["Data_Session", segment.SessionId],
                .. Flag(segment.SelfMute, localizer["Data_SelfMute"]),
                .. Flag(segment.SelfDeaf, localizer["Data_SelfDeaf"]),
                .. Flag(segment.Streaming, localizer["Data_Streaming"]),
                .. Flag(segment.Video, localizer["Data_Video"]),
                segment.EndedAt is { } ended ? Until(localizer, ended, row.OccurredAt) : localizer["Data_Open"],
            ],
            { PresenceActivity: { } activity } => [activity.ActivityName, Until(localizer, activity.EndedAt, row.OccurredAt)],
            _ => [],
        };

        var text = string.Join(Separator, parts.Where(p => !string.IsNullOrEmpty(p)));
        return text.Length == 0 ? UiCulture.Placeholder : text;
    }

    /// <summary>A user's display name; "Unbekannt" before the guild sync named them.</summary>
    public static string UserName(IStringLocalizer localizer, EventUserDto user)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(user);
        return user.DisplayName ?? localizer["Data_Unknown"];
    }

    /// <summary>A channel's name; "–" for rows without a channel, "Unbekannt" before the guild sync named it.</summary>
    public static string ChannelName(IStringLocalizer localizer, EventChannelDto? channel)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return channel is null ? UiCulture.Placeholder : channel.Name ?? localizer["Data_Unknown"];
    }

    private static List<string> MessageDetails(IStringLocalizer localizer, MessageEventDetailsDto message)
    {
        var parts = new List<string>();
        if (message.Length is { } length)
        {
            parts.Add(localizer["Data_Characters", length.ToString("N0", UiCulture.Culture)]);
        }

        if (message.WordCount is { } words)
        {
            parts.Add(localizer.Count(words, "Data_Words_One", "Data_Words"));
        }

        AddCount(parts, localizer, message.AttachmentCount, "Data_Attachments_One", "Data_Attachments");
        AddCount(parts, localizer, message.EmbedCount, "Data_Embeds_One", "Data_Embeds");
        AddCount(parts, localizer, message.EmojiCount, "Data_Emojis_One", "Data_Emojis");
        AddCount(parts, localizer, (message.UserMentionCount ?? 0) + (message.RoleMentionCount ?? 0), "Data_Mentions_One", "Data_Mentions");
        if (message.MentionsEveryone == true)
        {
            parts.Add(localizer["Data_MentionsEveryone"]);
        }

        if (message.Parts is { Count: > 0 } contentParts)
        {
            parts.Add(string.Join(", ", contentParts.Select(p => Part(localizer, p))));
        }

        if (message.ThreadParent is { } parent)
        {
            parts.Add(localizer["Data_ThreadIn", ChannelName(localizer, parent)]);
        }

        if (message.ReplyTo is { } replyTo)
        {
            parts.Add(localizer["Data_ReplyTo", UserName(localizer, replyTo)]);
        }

        if (message.IsBot)
        {
            parts.Add(localizer["Data_Bot"]);
        }

        return parts;
    }

    private static void AddCount(List<string> parts, IStringLocalizer localizer, int? count, string oneKey, string otherKey)
    {
        if (count is > 0)
        {
            parts.Add(localizer.Count(count.Value, oneKey, otherKey));
        }
    }

    private static string[] Flag(bool set, string text) => set ? [text] : [];

    /// <summary>"bis 11:35", with the date when the end is on another Zurich day than the start.</summary>
    private static string Until(IStringLocalizer localizer, DateTimeOffset end, DateTimeOffset start) =>
        localizer["Data_Until", UiTime.FormatSince(end, start)];

    private static string Kind(IStringLocalizer localizer, string kind) => kind switch
    {
        "Create" => localizer["Data_Kind_Create"],
        "Edit" => localizer["Data_Kind_Edit"],
        "Delete" => localizer["Data_Kind_Delete"],
        "Add" => localizer["Data_Kind_Add"],
        "Remove" => localizer["Data_Kind_Remove"],
        "Join" => localizer["Data_Kind_Join"],
        "Leave" => localizer["Data_Kind_Leave"],
        "Ban" => localizer["Data_Kind_Ban"],
        "Unban" => localizer["Data_Kind_Unban"],
        _ => kind,
    };

    private static string Activity(IStringLocalizer localizer, string activityType) => activityType switch
    {
        "Playing" => localizer["Data_Activity_Playing"],
        "Streaming" => localizer["Data_Activity_Streaming"],
        "Listening" => localizer["Data_Activity_Listening"],
        "Watching" => localizer["Data_Activity_Watching"],
        "Competing" => localizer["Data_Activity_Competing"],
        _ => activityType,
    };

    private static string EndReason(IStringLocalizer localizer, string? endReason) => endReason switch
    {
        "Left" => localizer["Data_EndReason_Left"],
        "Moved" => localizer["Data_EndReason_Moved"],
        "TrackingStopped" => localizer["Data_EndReason_TrackingStopped"],
        "BotRestart" => localizer["Data_EndReason_BotRestart"],
        _ => endReason ?? string.Empty,
    };

    private static string Part(IStringLocalizer localizer, string part) => part switch
    {
        "Text" => localizer["Data_Part_Text"],
        "Link" => localizer["Data_Part_Link"],
        "Mention" => localizer["Data_Part_Mention"],
        "Emoji" => localizer["Data_Part_Emoji"],
        "Attachment" => localizer["Data_Part_Attachment"],
        _ => part,
    };
}
