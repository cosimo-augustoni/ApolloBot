using System.Globalization;
using System.Runtime.CompilerServices;
using Apollo.Platform.Abstractions;

namespace Apollo.Platform.Discord;

/// <summary>
/// Converts between Discord snowflakes and <see cref="PlatformRef"/>.
/// </summary>
internal static class DiscordIds
{
    public static PlatformRef ToRef(ulong snowflake) => new(PlatformKind.Discord, snowflake.ToString(CultureInfo.InvariantCulture));

    /// <exception cref="ArgumentException">The ref is not a Discord snowflake.</exception>
    public static ulong ToSnowflake(PlatformRef platformRef, [CallerArgumentExpression(nameof(platformRef))] string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(platformRef, parameterName);

        if (platformRef.Platform != PlatformKind.Discord
            || !ulong.TryParse(platformRef.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out var snowflake))
        {
            throw new ArgumentException($"{platformRef} is not a Discord snowflake.", parameterName);
        }

        return snowflake;
    }
}
