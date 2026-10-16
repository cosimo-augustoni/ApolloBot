namespace Apollo.Domain.Tracking;

/// <summary>
/// The tracking role setting of one <c>(guild, platform)</c>, as <see cref="TrackingRule"/> needs it.
/// </summary>
/// <param name="TrackingRoleId">The internal ID of the configured tracking role; <see langword="null"/> when none is set.</param>
/// <param name="TrackingRoleDeleted">Whether the configured tracking role was deleted on the platform.</param>
/// <param name="TrackingRoleEverConfigured">
/// Whether a tracking role was ever configured (an <c>Added</c> entry in <c>role_setting_changes</c>). Only keeps
/// bootstrap from coming back once it ended. TEMPORARY: removed by #62.
/// </param>
public sealed record TrackingSettings(Guid? TrackingRoleId, bool TrackingRoleDeleted, bool TrackingRoleEverConfigured);
