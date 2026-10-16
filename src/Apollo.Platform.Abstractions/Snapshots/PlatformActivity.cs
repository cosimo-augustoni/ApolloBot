namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// Something a member is doing, as shown in their presence (Discord only, see
/// <see cref="PlatformCapabilities.PresenceActivities"/>).
/// </summary>
/// <param name="Type">The activity kind.</param>
/// <param name="Name">The activity name, e.g. the game title.</param>
/// <param name="StartedAt">When the activity started, if the platform reports it.</param>
public sealed record PlatformActivity(PlatformActivityType Type, string Name, DateTimeOffset? StartedAt);
