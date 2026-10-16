using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Platform.Abstractions.Tests.Commands;

public sealed class CommandDefinitionTests
{
    private static readonly CommandDefinition _tracking = new("apollo", "Apollo")
    {
        Groups = [new SubcommandGroupDefinition("tracking", "Tracking", [new("on", "An"), new("off", "Aus")])],
    };

    [Fact]
    public void Paths_ShouldListEveryGroupSubcommandAndSubcommand()
    {
        var definition = _tracking with { Subcommands = [new SubcommandDefinition("stats", "Statistik")] };

        definition.Paths.ShouldBe(["apollo tracking on", "apollo tracking off", "apollo stats"]);
    }

    [Fact]
    public void Paths_WhenCommandHasNoSubcommands_ShouldBeTheName()
    {
        new CommandDefinition("ping", "Ping").Paths.ShouldBe(["ping"]);
    }

    [Fact]
    public void Combine_WhenNamesDiffer_ShouldKeepEachCommand()
    {
        var ping = new CommandDefinition("ping", "Ping");

        CommandDefinition.Combine([_tracking, ping]).ShouldBe([_tracking, ping]);
    }

    [Fact]
    public void Combine_WhenNamesMatch_ShouldMergeGroupsAndSubcommandsIntoOneCommand()
    {
        var privacy = new CommandDefinition("apollo", "ignored")
        {
            Groups = [new SubcommandGroupDefinition("privacy", "Datenschutz", [new("delete", "Löschen")])],
            Subcommands = [new SubcommandDefinition("stats", "Statistik")],
        };

        var combined = CommandDefinition.Combine([_tracking, privacy]).ShouldHaveSingleItem();

        combined.Description.ShouldBe("Apollo");
        combined.Paths.ShouldBe(["apollo tracking on", "apollo tracking off", "apollo privacy delete", "apollo stats"]);
    }

    [Fact]
    public void Combine_WhenGroupNamesMatch_ShouldMergeTheirSubcommands()
    {
        var status = new CommandDefinition("apollo", "Apollo")
        {
            Groups = [new SubcommandGroupDefinition("tracking", "ignored", [new("status", "Status")])],
        };

        var combined = CommandDefinition.Combine([_tracking, status]).ShouldHaveSingleItem();

        combined.Groups.ShouldHaveSingleItem().Description.ShouldBe("Tracking");
        combined.Paths.ShouldBe(["apollo tracking on", "apollo tracking off", "apollo tracking status"]);
    }

    [Fact]
    public void Combine_WhenTwoDefinitionsDeclareTheSamePath_ShouldThrow()
    {
        var exception = Should.Throw<InvalidOperationException>(() => CommandDefinition.Combine([_tracking, _tracking]));

        exception.Message.ShouldContain("apollo tracking on");
    }
}
