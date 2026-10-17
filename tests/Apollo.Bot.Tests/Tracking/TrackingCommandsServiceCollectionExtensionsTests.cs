using Apollo.Bot.Commands;
using Apollo.Testing.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apollo.Bot.Tests.Tracking;

public sealed class TrackingCommandsServiceCollectionExtensionsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task AddTrackingCommands_ShouldDefineEveryHandlerPathAndHandleEveryDefinedPath()
    {
        await using var host = TrackingCommandHost.Create(Fixture);
        var validator = host.Services.GetServices<IHostedService>().OfType<CommandCatalogValidator>().Single();

        await validator.StartingAsync(TestContext.Current.CancellationToken);

        host.Logs.GetSnapshot().ShouldNotContain(r => r.Level >= LogLevel.Warning);
    }
}
