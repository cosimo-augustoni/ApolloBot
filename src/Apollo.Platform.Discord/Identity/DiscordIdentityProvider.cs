using System.Security.Claims;
using System.Text.Json;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;

namespace Apollo.Platform.Discord.Identity;

/// <summary>
/// Discord login (scope §6.1). The OAuth handler (<see cref="DiscordIdentityServiceCollectionExtensions"/>) reads
/// <c>/users/@me</c>, maps it with <see cref="MapUser"/> and stores only <see cref="ToClaims"/> in the external
/// cookie; <see cref="GetIdentity"/> reads them back.
/// </summary>
internal sealed class DiscordIdentityProvider : IPlatformIdentityProvider
{
    public const string Scheme = "Discord";

    private const string CdnBaseUrl = "https://cdn.discordapp.com";
    private const string AvatarUrlClaimType = "urn:apollo:discord:avatar_url";

    public PlatformKind Platform => PlatformKind.Discord;

    public string SchemeName => Scheme;

    /// <summary>Maps Discord's user object (<c>GET /users/@me</c>) to a neutral identity.</summary>
    public static PlatformIdentity MapUser(JsonElement user)
    {
        var id = user.GetProperty("id").GetString()!;
        var displayName = OptionalString(user, "global_name") ?? user.GetProperty("username").GetString()!;
        var avatarHash = OptionalString(user, "avatar");

        return new PlatformIdentity(new PlatformRef(PlatformKind.Discord, id), displayName, AvatarUrl(id, avatarHash));
    }

    /// <summary>The claims the external cookie holds for <paramref name="identity"/>; no token.</summary>
    public static IEnumerable<Claim> ToClaims(PlatformIdentity identity)
    {
        yield return new Claim(ClaimTypes.NameIdentifier, identity.User.ExternalId);
        yield return new Claim(ClaimTypes.Name, identity.DisplayName);

        if (identity.AvatarUrl is not null)
        {
            yield return new Claim(AvatarUrlClaimType, identity.AvatarUrl);
        }
    }

    public PlatformIdentity? GetIdentity(ClaimsPrincipal externalPrincipal)
    {
        ArgumentNullException.ThrowIfNull(externalPrincipal);

        var identity = externalPrincipal.Identities.FirstOrDefault(i => i.AuthenticationType == Scheme);
        var id = identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var displayName = identity?.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return new PlatformIdentity(new PlatformRef(PlatformKind.Discord, id), displayName, identity!.FindFirst(AvatarUrlClaimType)?.Value);
    }

    // Image formatting: https://discord.com/developers/docs/reference#image-formatting (a_ hashes are animated).
    private static string? AvatarUrl(string userId, string? avatarHash) =>
        avatarHash is null
            ? null
            : $"{CdnBaseUrl}/avatars/{userId}/{avatarHash}.{(avatarHash.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png")}";

    private static string? OptionalString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;
}
