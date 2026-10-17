using Apollo.Bot.Commands;
using Apollo.Bot.Stats;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apollo.Bot.Tests.Stats;

public sealed class StatsCommandsServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddStatsCommands_ShouldDefineEveryHandlerPathAndHandleEveryDefinedPath()
    {
        using var host = StatsCommandHost.Create("https://apollo.example.ch");
        var validator = host.Services.GetServices<IHostedService>().OfType<CommandCatalogValidator>().Single();

        await validator.StartingAsync(TestContext.Current.CancellationToken);

        host.Logs.GetSnapshot().ShouldNotContain(r => r.Level >= LogLevel.Warning);
    }

    [Fact]
    public void Definition_WhenCombinedWithTheTrackingCommands_ShouldBeOneApolloCommandWithBothParts()
    {
        var combined = CommandDefinition.Combine(
            [TrackingCommandsServiceCollectionExtensions.Definition, StatsCommandsServiceCollectionExtensions.Definition]);

        combined.ShouldHaveSingleItem().Paths.ShouldBe(
            ["apollo tracking on", "apollo tracking off", "apollo tracking status", "apollo stats"],
            ignoreOrder: true);
    }
}
