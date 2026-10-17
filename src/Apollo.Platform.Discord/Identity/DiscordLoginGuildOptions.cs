using System.ComponentModel.DataAnnotations;

namespace Apollo.Platform.Discord.Identity;

/// <summary>
/// The guild whose members may use the web login (#30), bound from <c>Apollo:Platform:Discord</c>: the same
/// <c>GuildId</c> key the bot reads (<see cref="DiscordPlatformOptions"/>), without the bot's token.
/// </summary>
public sealed class DiscordLoginGuildOptions
{
    public const string SectionName = DiscordPlatformOptions.SectionName;

    /// <summary>The one guild the bot records (scope §4.3). Admin access is the Administrator permission there.</summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(GuildId)} is required.")]
    public ulong? GuildId { get; set; }
}
