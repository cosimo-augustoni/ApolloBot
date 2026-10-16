namespace Apollo.Platform.Abstractions;

/// <summary>
/// Identifies a user, guild, channel, role, message or custom emoji on one platform. Outside the adapters this is the
/// only way to reference platform objects; the bot maps it to internal IDs (scope §4.2).
/// </summary>
/// <param name="Platform">The platform the ID belongs to. The same ID on another platform is a different object.</param>
/// <param name="ExternalId">The platform's own ID (a snowflake on Discord), kept as text so every platform fits.</param>
public sealed record PlatformRef(PlatformKind Platform, string ExternalId)
{
    public string ExternalId { get; init => field = NotBlank(value); } = NotBlank(ExternalId);

    public override string ToString() => $"{Platform}:{ExternalId}";

    private static string NotBlank(string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId, nameof(ExternalId));
        return externalId;
    }
}
