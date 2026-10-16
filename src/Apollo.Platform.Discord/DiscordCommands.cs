using Apollo.Platform.Abstractions.Commands;
using NetCord;
using NetCord.Rest;

namespace Apollo.Platform.Discord;

/// <summary>
/// Turns the neutral <see cref="CommandDefinition"/>s into NetCord slash commands, so handlers never declare NetCord
/// command modules.
/// </summary>
internal static class DiscordCommands
{
    /// <summary>
    /// The slash command for <paramref name="definition"/>, offered in guilds only (not in DMs with the bot).
    /// </summary>
    public static SlashCommandProperties ToProperties(CommandDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new SlashCommandProperties(definition.Name, definition.Description)
        {
            Contexts = [InteractionContextType.Guild],
            Options =
            [
                .. definition.Groups.Select(ToProperties),
                .. definition.Subcommands.Select(ToProperties),
                .. definition.Options.Select(ToProperties),
            ],
        };
    }

    private static ApplicationCommandOptionProperties ToProperties(SubcommandGroupDefinition group) =>
        new(ApplicationCommandOptionType.SubCommandGroup, group.Name, group.Description)
        {
            Options = [.. group.Subcommands.Select(ToProperties)],
        };

    private static ApplicationCommandOptionProperties ToProperties(SubcommandDefinition subcommand) =>
        new(ApplicationCommandOptionType.SubCommand, subcommand.Name, subcommand.Description)
        {
            Options = [.. subcommand.Options.Select(ToProperties)],
        };

    private static ApplicationCommandOptionProperties ToProperties(CommandOptionDefinition option) =>
        new(ToOptionType(option.Type), option.Name, option.Description) { Required = option.Required };

    private static ApplicationCommandOptionType ToOptionType(CommandOptionType type) => type switch
    {
        CommandOptionType.String => ApplicationCommandOptionType.String,
        CommandOptionType.Integer => ApplicationCommandOptionType.Integer,
        CommandOptionType.Boolean => ApplicationCommandOptionType.Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown command option type."),
    };
}
