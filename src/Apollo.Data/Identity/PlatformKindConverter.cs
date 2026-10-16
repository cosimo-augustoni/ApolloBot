using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apollo.Data.Identity;

/// <summary>
/// Stores <see cref="PlatformKind"/> as lower-case text (<c>discord</c>, <c>fluxer</c>) in every mapping table, so the
/// rows stay readable in SQL and do not depend on the enum's numeric values.
/// </summary>
internal sealed class PlatformKindConverter() : ValueConverter<PlatformKind, string>(
    platform => ToText(platform),
    text => FromText(text))
{
    public static string ToText(PlatformKind platform) => platform switch
    {
        PlatformKind.Discord => "discord",
        PlatformKind.Fluxer => "fluxer",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform."),
    };

    public static PlatformKind FromText(string text) => text switch
    {
        "discord" => PlatformKind.Discord,
        "fluxer" => PlatformKind.Fluxer,
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, "Unknown platform."),
    };
}
