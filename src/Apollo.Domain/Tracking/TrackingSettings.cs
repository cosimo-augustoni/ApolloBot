namespace Apollo.Domain.Tracking;

/// <summary>
/// The tracking role setting of one <c>(guild, platform)</c>, as <see cref="TrackingRule"/> needs it.
/// </summary>
/// <param name="TrackingRoleId">The internal ID of the configured tracking role; <see langword="null"/> when none is set.</param>
/// <param name="TrackingRoleDeleted">Whether the configured tracking role was deleted on the platform.</param>
public sealed record TrackingSettings(Guid? TrackingRoleId, bool TrackingRoleDeleted);
