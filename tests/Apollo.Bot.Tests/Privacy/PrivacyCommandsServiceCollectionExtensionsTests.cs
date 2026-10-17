using Apollo.Bot.Commands;
using Apollo.Bot.Privacy;
using Apollo.Bot.Stats;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Testing.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apollo.Bot.Tests.Privacy;

public sealed class PrivacyCommandsServiceCollectionExtensionsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task AddPrivacyCommands_ShouldDefineEveryHandlerPathAndHandleEveryDefinedPath()
    {
        await using var host = PrivacyCommandHost.CreatePrivacyHost(Fixture);
        var validator = host.Services.GetServices<IHostedService>().OfType<CommandCatalogValidator>().Single();

        await validator.StartingAsync(TestContext.Current.CancellationToken);

        host.Logs.GetSnapshot().ShouldNotContain(r => r.Level >= LogLevel.Warning);
    }

    [Fact]
    public void Definition_WhenCombinedWithTheOtherParts_ShouldBeOneApolloCommandWithTheSharedDescription()
    {
        var combined = CommandDefinition.Combine(
        [
            PrivacyCommandsServiceCollectionExtensions.Definition,
            TrackingCommandsServiceCollectionExtensions.Definition,
            StatsCommandsServiceCollectionExtensions.Definition,
        ]).ShouldHaveSingleItem();

        combined.Description.ShouldBe(ApolloCommand.Description);
        combined.Paths.ShouldBe(
            ["apollo privacy delete", "apollo tracking on", "apollo tracking off", "apollo tracking status", "apollo stats"],
            ignoreOrder: true);
    }
}
