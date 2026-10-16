using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// The guild became available after connecting: a full snapshot for the guild sync (#13).
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Name">The guild name.</param>
/// <param name="Roles">All roles.</param>
/// <param name="Channels">All channels, categories and threads.</param>
/// <param name="Members">The members the platform included. May be incomplete (Fluxer sends almost none), so the sync follows up with <see cref="IChatPlatform.ListMembersAsync"/>.</param>
/// <param name="VoiceStates">The current voice states.</param>
/// <param name="Presences">The current presences, if the platform includes them.</param>
public sealed record PlatformGuildAvailable(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    string Name,
    IReadOnlyList<PlatformRole> Roles,
    IReadOnlyList<PlatformChannel> Channels,
    IReadOnlyList<PlatformMember> Members,
    IReadOnlyList<PlatformVoiceState> VoiceStates,
    IReadOnlyList<PlatformPresence> Presences)
    : PlatformEvent(Guild, OccurredAt);
