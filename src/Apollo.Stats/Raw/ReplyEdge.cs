namespace Apollo.Stats.Raw;

/// <summary>
/// <see cref="Count"/> messages by <see cref="AuthorId"/> that reply to a message by <see cref="RepliedToId"/>.
/// </summary>
public sealed record ReplyEdge(Guid AuthorId, Guid RepliedToId, int Count);
