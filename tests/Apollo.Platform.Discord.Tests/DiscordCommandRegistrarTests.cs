using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Discord.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordCommandRegistrarTests : IDisposable
{
    private static readonly CommandDefinition _tracking = new("apollo", "Apollo-Befehle")
    {
        Groups = [new SubcommandGroupDefinition("tracking", "Erfassung", [new("status", "Status anzeigen")])],
    };

    private static readonly CommandDefinition _stats = new("apollo", "ignored")
    {
        Subcommands = [new SubcommandDefinition("stats", "Deine Statistik")],
    };

    private readonly TestGateway _gateway = new();
    private readonly FakeLogger<DiscordCommandRegistrar> _logger = new();

    public DiscordCommandRegistrarTests() => _gateway.Rest.Respond = _ => "[]";

    [Fact]
    public async Task HandleAsync_WhenGatewayIsReady_ShouldOverwriteTheCommandsOfTheConfiguredGuildOnly()
    {
        await CreateRegistrar(_tracking).HandleAsync(NetCordPayloads.Ready());

        var request = _gateway.Rest.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Put);
        request.Path.ShouldEndWith("/applications/999/guilds/111111111111111111/commands");
    }

    [Fact]
    public async Task HandleAsync_ShouldRegisterTheDefinitionsMergedByName()
    {
        await CreateRegistrar(_tracking, _stats).HandleAsync(NetCordPayloads.Ready());

        var body = _gateway.Rest.Bodies.ShouldHaveSingleItem().ShouldNotBeNull();
        body.ShouldStartWith("[{");
        body.Split("\"name\":\"apollo\"").Length.ShouldBe(2, "one apollo command");
        body.ShouldContain("\"name\":\"tracking\"");
        body.ShouldContain("\"name\":\"status\"");
        body.ShouldContain("\"name\":\"stats\"");
    }

    [Fact]
    public async Task HandleAsync_WhenNoCommandIsDefined_ShouldClearTheGuildCommands()
    {
        await CreateRegistrar().HandleAsync(NetCordPayloads.Ready());

        _gateway.Rest.Bodies.ShouldHaveSingleItem().ShouldBe("[]");
    }

    [Fact]
    public async Task HandleAsync_WhenRegistrationFails_ShouldLogTheErrorWithoutThrowing()
    {
        _gateway.Rest.Respond = _ => "not json";

        await CreateRegistrar(_tracking).HandleAsync(NetCordPayloads.Ready());

        _logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Error && log.Exception != null);
    }

    public void Dispose() => _gateway.Dispose();

    private DiscordCommandRegistrar CreateRegistrar(params CommandDefinition[] definitions) => new(
        _gateway.Client,
        definitions,
        Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId }),
        _logger);
}
