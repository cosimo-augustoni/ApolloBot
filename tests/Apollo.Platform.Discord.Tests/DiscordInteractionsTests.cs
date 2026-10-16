using Apollo.Platform.Abstractions;
using Apollo.Platform.Discord.Tests.Fakes;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordInteractionsTests : IDisposable
{
    private static readonly PlatformRef _guild = new(PlatformKind.Discord, "111111111111111111");

    private readonly NetCordInteractions _interactions = new();

    [Fact]
    public void ToInvocation_WhenSlashCommandHasGroupAndSubcommand_ShouldJoinThemIntoThePath()
    {
        var interaction = _interactions.SlashCommand(DiscordHost.GuildId, userId: 42, "apollo", """
            [ { "name": "tracking", "type": 2, "options": [ { "name": "status", "type": 1, "options": [] } ] } ]
            """);

        var invocation = DiscordInteractions.ToInvocation(interaction, _guild);

        invocation.Path.ShouldBe("apollo tracking status");
        invocation.Options.ShouldBeEmpty();
    }

    [Fact]
    public void ToInvocation_ShouldCarryGuildMemberAndTime()
    {
        var interaction = _interactions.SlashCommand(DiscordHost.GuildId, userId: 42, "apollo", """[ { "name": "stats", "type": 1 } ]""");

        var invocation = DiscordInteractions.ToInvocation(interaction, _guild);

        invocation.Guild.ShouldBe(_guild);
        invocation.Member.ShouldBe(new PlatformRef(PlatformKind.Discord, "42"));
        invocation.At.ShouldBe(interaction.CreatedAt);
    }

    [Fact]
    public void ToInvocation_WhenSubcommandHasOptions_ShouldConvertTheirValuesByType()
    {
        var interaction = _interactions.SlashCommand(DiscordHost.GuildId, userId: 42, "apollo", """
            [ { "name": "stats", "type": 1, "options": [
                { "name": "days", "type": 4, "value": 7 },
                { "name": "public", "type": 5, "value": true },
                { "name": "channel", "type": 3, "value": "general" }
            ] } ]
            """);

        var invocation = DiscordInteractions.ToInvocation(interaction, _guild);

        invocation.Path.ShouldBe("apollo stats");
        invocation.Options.ShouldBe(new Dictionary<string, object?> { ["days"] = 7L, ["public"] = true, ["channel"] = "general" });
    }

    [Fact]
    public void ToInvocation_WhenCommandHasNoSubcommand_ShouldUseTheNameAndItsOptions()
    {
        var interaction = _interactions.SlashCommand(DiscordHost.GuildId, userId: 42, "ping", """[ { "name": "times", "type": 4, "value": 3 } ]""");

        var invocation = DiscordInteractions.ToInvocation(interaction, _guild);

        invocation.Path.ShouldBe("ping");
        invocation.Options.ShouldBe(new Dictionary<string, object?> { ["times"] = 3L });
    }

    [Fact]
    public void ToInvocation_WhenButtonIsClicked_ShouldCarryTheButtonIdMemberAndTime()
    {
        var interaction = _interactions.ButtonClick(DiscordHost.GuildId, userId: 42, "privacy-delete:42");

        var invocation = DiscordInteractions.ToInvocation(interaction, _guild);

        invocation.ShouldBe(new(_guild, new PlatformRef(PlatformKind.Discord, "42"), "privacy-delete:42", interaction.CreatedAt));
    }

    public void Dispose() => _interactions.Dispose();
}
