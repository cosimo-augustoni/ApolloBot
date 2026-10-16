using System.ComponentModel.DataAnnotations;

namespace Apollo.Platform.Discord.Identity;

/// <summary>
/// The Discord OAuth2 application used for the web login, bound from <c>Apollo:Platform:Discord:OAuth</c>.
/// The redirect URI to register is <c>&lt;PublicWebUrl&gt;/signin-discord</c> (docs/deployment.md).
/// </summary>
public sealed class DiscordOAuthOptions
{
    public const string SectionName = "Apollo:Platform:Discord:OAuth";

    /// <summary>The application's client ID from the Discord developer portal.</summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(ClientId)} is required.")]
    public string? ClientId { get; set; }

    /// <summary>
    /// The client secret. A secret: user-secrets in development, <c>Apollo__Platform__Discord__OAuth__ClientSecret</c>
    /// in production.
    /// </summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(ClientSecret)} is required.")]
    public string? ClientSecret { get; set; }
}
