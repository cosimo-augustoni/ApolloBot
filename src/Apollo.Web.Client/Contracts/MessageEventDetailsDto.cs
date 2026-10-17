namespace Apollo.Web.Client.Contracts;

/// <summary>
/// The metadata of a <c>message_events</c> row (scope §5.1, #15). The counts and <see cref="Parts"/> are
/// <see langword="null"/> on delete rows.
/// </summary>
/// <param name="Kind">As stored: <c>Create</c>, <c>Edit</c> or <c>Delete</c>.</param>
/// <param name="Length">Characters as shown.</param>
/// <param name="WordCount">Words of the text part.</param>
/// <param name="AttachmentCount">Attachments.</param>
/// <param name="EmbedCount">Embeds.</param>
/// <param name="EmojiCount">Unicode and custom emoji.</param>
/// <param name="UserMentionCount">Mentioned users.</param>
/// <param name="RoleMentionCount">Mentioned roles.</param>
/// <param name="MentionsEveryone">Whether it mentions <c>@everyone</c> or <c>@here</c>.</param>
/// <param name="Parts">The flags of <c>content_parts</c> by name, e.g. <c>Text</c>, <c>Link</c>.</param>
/// <param name="ThreadParent">For a message in a thread, the thread's parent channel.</param>
/// <param name="ReplyTo">The replied-to author if tracked; <see langword="null"/> when not a reply or untracked.</param>
/// <param name="IsBot">Whether a bot sent it.</param>
public sealed record MessageEventDetailsDto(
    string Kind,
    int? Length,
    int? WordCount,
    int? AttachmentCount,
    int? EmbedCount,
    int? EmojiCount,
    int? UserMentionCount,
    int? RoleMentionCount,
    bool? MentionsEveryone,
    IReadOnlyList<string>? Parts,
    EventChannelDto? ThreadParent,
    EventUserDto? ReplyTo,
    bool IsBot);
