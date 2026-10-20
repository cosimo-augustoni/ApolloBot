namespace Apollo.Domain.Presence;

/// <summary>
/// One activity as a presence shows it now: the session key and its rich presence.
/// </summary>
public sealed record ObservedActivity(PresenceActivity Activity, PresenceActivityDetails Details);
