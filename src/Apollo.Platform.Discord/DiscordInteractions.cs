using System.Globalization;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using NetCord;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps NetCord slash-command and button interactions to the neutral command model. Only for interactions of the
/// configured guild; the caller filters (<see cref="DiscordEventMapper.IsConfiguredGuild"/>).
/// </summary>
internal static class DiscordInteractions
{
    /// <summary>
    /// The path is the command name plus its subcommand group and subcommand; the options are those of the innermost
    /// one.
    /// </summary>
    public static CommandInvocation ToInvocation(SlashCommandInteraction interaction, PlatformRef guild)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        List<string> path = [interaction.Data.Name];
        var options = interaction.Data.Options;
        while (options is [{ Type: ApplicationCommandOptionType.SubCommandGroup or ApplicationCommandOptionType.SubCommand } nested])
        {
            path.Add(nested.Name);
            options = nested.Options ?? [];
        }

        return new CommandInvocation(
            guild,
            DiscordIds.ToRef(interaction.User.Id),
            string.Join(' ', path),
            options.ToDictionary(option => option.Name, ToValue, StringComparer.Ordinal),
            interaction.CreatedAt);
    }

    public static ButtonInvocation ToInvocation(ButtonInteraction interaction, PlatformRef guild)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        return new ButtonInvocation(guild, DiscordIds.ToRef(interaction.User.Id), interaction.Data.CustomId, interaction.CreatedAt);
    }

    // The values the neutral CommandOptionType declares; anything else stays the raw text Discord sent.
    private static object? ToValue(ApplicationCommandInteractionDataOption option) => option.Type switch
    {
        ApplicationCommandOptionType.Integer => long.Parse(option.Value!, NumberStyles.Integer, CultureInfo.InvariantCulture),
        ApplicationCommandOptionType.Boolean => bool.Parse(option.Value!),
        _ => option.Value,
    };
}
