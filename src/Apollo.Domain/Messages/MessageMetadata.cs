namespace Apollo.Domain.Messages;

/// <summary>
/// What ApolloBot keeps about a message's content: counts and flags, never the text (scope §5.1). Adapters compute it
/// in memory and drop the content.
/// </summary>
/// <remarks>
/// Placeholder until #15 adds the fields and the pure function that computes them. Add fields as <c>init</c>
/// properties, so existing constructions keep compiling.
/// </remarks>
public sealed record MessageMetadata
{
    public static MessageMetadata Empty { get; } = new();
}
