namespace Apollo.Platform.Abstractions;

/// <summary>
/// A chat platform ApolloBot can record. Each has its own adapter project (<c>Apollo.Platform.*</c>).
/// </summary>
/// <remarks>
/// Not named <c>Platform</c>: inside any <c>Apollo.*</c> namespace that name binds to the <c>Apollo.Platform</c>
/// namespace first.
/// </remarks>
public enum PlatformKind
{
    Discord,
    Fluxer,
}
