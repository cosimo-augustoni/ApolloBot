namespace Apollo.Stats.Raw;

/// <summary>
/// How often an emoji was added as a reaction. A unicode emoji has its <see cref="Name"/> (the emoji itself) and no
/// <see cref="CustomId"/>; a custom emoji has its platform ID and its latest known name (empty when the platform no
/// longer knows it, #17).
/// </summary>
public sealed record EmojiUsage(string Name, string? CustomId, int Count);
