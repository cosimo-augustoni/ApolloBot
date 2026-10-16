using Apollo.Platform.Abstractions;

namespace Apollo.Data.Identity;

/// <summary>
/// Maps <see cref="PlatformRef"/>s to internal IDs (scope §4.2). <c>Resolve*</c> creates the internal row and its
/// mapping on first sight and is safe under concurrent calls from several processes; <c>TryGet*</c> never creates
/// anything, for callers that must not store data about an object (e.g. references to untracked users, scope §5.1.1).
/// </summary>
public interface IPlatformIdResolver
{
    Task<Guid> ResolveGuildAsync(PlatformRef guild, CancellationToken cancellationToken);

    Task<Guid> ResolveUserAsync(PlatformRef user, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the channel and, if needed, its guild. A new channel belongs to the internal guild of
    /// <paramref name="guild"/>; both refs must be on the same platform.
    /// </summary>
    Task<Guid> ResolveChannelAsync(PlatformRef guild, PlatformRef channel, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the role and, if needed, its guild. A new role belongs to the internal guild of
    /// <paramref name="guild"/>; both refs must be on the same platform.
    /// </summary>
    Task<Guid> ResolveRoleAsync(PlatformRef guild, PlatformRef role, CancellationToken cancellationToken);

    /// <summary>
    /// The internal guild ID, or <see langword="null"/> when the guild has never been resolved. Creates nothing.
    /// </summary>
    Task<Guid?> TryGetGuildAsync(PlatformRef guild, CancellationToken cancellationToken);

    /// <summary>
    /// The internal user ID, or <see langword="null"/> when the user has never been resolved. Creates nothing.
    /// </summary>
    Task<Guid?> TryGetUserAsync(PlatformRef user, CancellationToken cancellationToken);

    /// <summary>
    /// The internal channel ID, or <see langword="null"/> when the channel has never been resolved. Creates nothing.
    /// </summary>
    Task<Guid?> TryGetChannelAsync(PlatformRef channel, CancellationToken cancellationToken);

    /// <summary>
    /// The internal role ID, or <see langword="null"/> when the role has never been resolved. Creates nothing.
    /// </summary>
    Task<Guid?> TryGetRoleAsync(PlatformRef role, CancellationToken cancellationToken);
}
