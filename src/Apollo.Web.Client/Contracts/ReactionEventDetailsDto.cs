namespace Apollo.Web.Client.Contracts;

/// <summary>A <c>reaction_events</c> row (scope §5.1, #17).</summary>
/// <param name="Kind">As stored: <c>Add</c> or <c>Remove</c>.</param>
/// <param name="EmojiName">The unicode emoji, or the custom emoji's name (empty when the platform no longer knows it).</param>
/// <param name="IsCustomEmoji">Whether it is a custom emoji of the platform.</param>
/// <param name="MessageAuthor">The message author if tracked; <see langword="null"/> when untracked or unknown.</param>
public sealed record ReactionEventDetailsDto(string Kind, string EmojiName, bool IsCustomEmoji, EventUserDto? MessageAuthor);
