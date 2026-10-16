using Apollo.Platform.Abstractions.Commands;
using NetCord;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordCommandsTests
{
    private static readonly CommandDefinition _apollo = new("apollo", "Apollo-Befehle")
    {
        Groups = [new SubcommandGroupDefinition("tracking", "Erfassung", [new("on", "Erfassung einschalten")])],
        Subcommands =
        [
            new SubcommandDefinition("stats", "Deine Statistik")
            {
                Options = [new CommandOptionDefinition("days", "Anzahl Tage", CommandOptionType.Integer, Required: true)],
            },
        ],
    };

    [Fact]
    public void ToProperties_ShouldKeepNameAndDescription()
    {
        var command = DiscordCommands.ToProperties(_apollo);

        command.Name.ShouldBe("apollo");
        command.Description.ShouldBe("Apollo-Befehle");
    }

    [Fact]
    public void ToProperties_ShouldMapGroupsToSubCommandGroupsWithTheirSubcommands()
    {
        var group = DiscordCommands.ToProperties(_apollo).Options.ShouldNotBeNull().First();

        group.Type.ShouldBe(ApplicationCommandOptionType.SubCommandGroup);
        group.Name.ShouldBe("tracking");
        group.Description.ShouldBe("Erfassung");
        var subcommand = group.Options.ShouldNotBeNull().ShouldHaveSingleItem();
        subcommand.Type.ShouldBe(ApplicationCommandOptionType.SubCommand);
        subcommand.Name.ShouldBe("on");
        subcommand.Description.ShouldBe("Erfassung einschalten");
    }

    [Fact]
    public void ToProperties_ShouldMapSubcommandsWithTheirOptions()
    {
        var subcommand = DiscordCommands.ToProperties(_apollo).Options.ShouldNotBeNull().Last();

        subcommand.Type.ShouldBe(ApplicationCommandOptionType.SubCommand);
        subcommand.Name.ShouldBe("stats");
        var option = subcommand.Options.ShouldNotBeNull().ShouldHaveSingleItem();
        option.Type.ShouldBe(ApplicationCommandOptionType.Integer);
        option.Name.ShouldBe("days");
        option.Description.ShouldBe("Anzahl Tage");
        option.Required.ShouldBe(true);
    }

    [Theory]
    [InlineData(CommandOptionType.String, ApplicationCommandOptionType.String)]
    [InlineData(CommandOptionType.Integer, ApplicationCommandOptionType.Integer)]
    [InlineData(CommandOptionType.Boolean, ApplicationCommandOptionType.Boolean)]
    public void ToProperties_ShouldMapOptionTypes(CommandOptionType type, ApplicationCommandOptionType expected)
    {
        var command = DiscordCommands.ToProperties(new CommandDefinition("ping", "Ping")
        {
            Options = [new CommandOptionDefinition("value", "Wert", type)],
        });

        command.Options.ShouldNotBeNull().ShouldHaveSingleItem().Type.ShouldBe(expected);
    }

    [Fact]
    public void ToProperties_ShouldOfferTheCommandInGuildsOnly()
    {
        DiscordCommands.ToProperties(_apollo).Contexts.ShouldBe([InteractionContextType.Guild]);
    }
}
