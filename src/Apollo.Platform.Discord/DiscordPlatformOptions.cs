using System.ComponentModel.DataAnnotations;

namespace Apollo.Platform.Discord;

/// <summary>
/// Settings of the Discord adapter, bound from <c>Apollo:Platform:Discord</c>.
/// </summary>
public sealed class DiscordPlatformOptions
{
    public const string SectionName = "Apollo:Platform:Discord";

    /// <summary>
    /// The bot token. A secret: user-secrets in development, <c>Apollo__Platform__Discord__Token</c> in production.
    /// </summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(Token)} is required.")]
    public string? Token { get; set; }

    /// <summary>
    /// The one guild the bot records (scope §4.3). Events from any other guild are dropped.
    /// </summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(GuildId)} is required.")]
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Whether the bot connects to the Discord gateway while it runs. <see langword="false"/> starts it without a
    /// connection, for the AppHost smoke tests (no live Discord in tests). Defaults to <see langword="true"/>.
    /// </summary>
    public bool Connect { get; set; } = true;
}
