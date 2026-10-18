namespace Apollo.Stats.Tracking;

/// <summary>
/// The tracking status of one member (scope §5.1.1): for the admin member page (#38), the member list (#101) and the
/// member area (#41).
/// </summary>
public interface ITrackingStatusService
{
    /// <summary>The status of <paramref name="userId"/> in <paramref name="guildId"/> (internal IDs).</summary>
    Task<TrackingStatus> GetAsync(Guid guildId, Guid userId, CancellationToken cancellationToken);
}
