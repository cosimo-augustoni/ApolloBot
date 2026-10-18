using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions;

/// <summary>
/// Outbound calls the bot makes to a platform. Only the operations ApolloBot needs (scope §4.2); inbound events
/// arrive through <see cref="Dispatch.IPlatformEventDispatcher"/> instead.
/// </summary>
/// <remarks>
/// Reads may be served from the adapter's gateway cache rather than a fresh REST call (Fluxer.Net, for example, has no
/// list-roles call). Callers must not rely on reading through to the platform.
/// </remarks>
public interface IChatPlatform
{
    PlatformKind Platform { get; }

    PlatformCapabilities Capabilities { get; }

    /// <summary>
    /// Whether the adapter has the guild's state right now: after the snapshot of the current gateway session arrived,
    /// and not while the platform reports the guild unavailable. Until then the reads served from the gateway cache
    /// (e.g. <see cref="ListVoiceStatesAsync"/>) may be empty, which callers must not take as "nobody is there".
    /// </summary>
    bool IsGuildAvailable(PlatformRef guild);

    /// <summary>
    /// The member, or <see langword="null"/> when the user is not (or no longer) in the guild.
    /// </summary>
    Task<PlatformMember?> GetMemberAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken);

    /// <summary>
    /// The roles the member holds; empty when the user is not in the guild.
    /// </summary>
    Task<IReadOnlyList<PlatformRef>> GetMemberRolesAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken);

    /// <summary>
    /// The member's effective guild permissions; <see cref="PlatformPermissions.None"/> when the user is not in the guild.
    /// </summary>
    Task<PlatformPermissions> GetMemberPermissionsAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken);

    /// <summary>
    /// All roles of the guild. May come from the gateway cache (see remarks on <see cref="IChatPlatform"/>).
    /// </summary>
    Task<IReadOnlyList<PlatformRole>> ListRolesAsync(PlatformRef guild, CancellationToken cancellationToken);

    /// <summary>
    /// All channels of the guild, including categories and threads.
    /// </summary>
    Task<IReadOnlyList<PlatformChannel>> ListChannelsAsync(PlatformRef guild, CancellationToken cancellationToken);

    /// <summary>
    /// All members of the guild. Adapters request the full member list from the platform on every call (the gateway
    /// snapshot is incomplete), so this is expensive: on Discord a gateway round trip that may take up to two minutes,
    /// on Fluxer at most one request per 30 s. Call it for reconciliation, not per event.
    /// </summary>
    /// <exception cref="TimeoutException">The platform did not deliver the list in time.</exception>
    Task<IReadOnlyList<PlatformMember>> ListMembersAsync(PlatformRef guild, CancellationToken cancellationToken);

    /// <summary>
    /// The current voice states. One user may have several entries on Fluxer (one per connection).
    /// </summary>
    Task<IReadOnlyList<PlatformVoiceState>> ListVoiceStatesAsync(PlatformRef guild, CancellationToken cancellationToken);

    /// <summary>
    /// The current presences. Their activities are empty when <see cref="PlatformCapabilities.PresenceActivities"/> is
    /// not supported, and the list itself may be empty (Fluxer).
    /// </summary>
    Task<IReadOnlyList<PlatformPresence>> ListPresencesAsync(PlatformRef guild, CancellationToken cancellationToken);

    /// <summary>
    /// Gives the member a role. The platform echoes the change as a <see cref="Events.PlatformMemberUpdated"/>.
    /// </summary>
    Task AddRoleAsync(PlatformRef guild, PlatformRef user, PlatformRef role, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a role from the member. The platform echoes the change as a <see cref="Events.PlatformMemberUpdated"/>.
    /// </summary>
    Task RemoveRoleAsync(PlatformRef guild, PlatformRef user, PlatformRef role, CancellationToken cancellationToken);
}
