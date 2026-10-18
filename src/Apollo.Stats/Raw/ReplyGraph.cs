namespace Apollo.Stats.Raw;

/// <summary>
/// The reply graph of a range. <see cref="Edges"/> are the top directed edges (for a user, those involving them);
/// for a user, <see cref="RepliesTo"/> are whom they reply to most and <see cref="RepliedBy"/> who replies to them
/// most, both empty for the guild. Every list is ordered by count, most first.
/// </summary>
public sealed record ReplyGraph(IReadOnlyList<ReplyEdge> Edges, IReadOnlyList<ReplyEdge> RepliesTo, IReadOnlyList<ReplyEdge> RepliedBy);
