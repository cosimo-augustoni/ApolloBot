using System.Collections.Concurrent;
using Apollo.Platform.Abstractions;

namespace Apollo.Data.Identity;

/// <summary>
/// Process-wide cache of resolved internal IDs. A mapping never changes once created, so entries never expire. Only
/// hits are cached: an unknown ref may be created by another process at any time.
/// </summary>
/// <remarks>
/// One guild's users, channels and roles fit in memory easily. Linking a Fluxer account to an existing user (M6) adds
/// a mapping but never changes one, so it does not invalidate entries either.
/// </remarks>
public sealed class PlatformIdCache
{
    private readonly ConcurrentDictionary<(IdentityKind Kind, PlatformRef Ref), Guid> _ids = new();

    internal bool TryGet(IdentityKind kind, PlatformRef platformRef, out Guid id) =>
        _ids.TryGetValue((kind, platformRef), out id);

    internal void Add(IdentityKind kind, PlatformRef platformRef, Guid id) => _ids[(kind, platformRef)] = id;
}
