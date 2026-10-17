namespace Apollo.Domain.Presence;

/// <summary>
/// One activity of a user, as far as sessions care: its kind and name (e.g. playing "Factorio"). Together with the user
/// it identifies an open session.
/// </summary>
public sealed record PresenceActivity(PresenceActivityType Type, string Name);
