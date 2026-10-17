using Apollo.Bot.Commands;
using Apollo.Bot.Links;
using Apollo.Bot.Stats;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Apollo.Bot.Tests.Stats;

/// <summary>
/// A host with only the real command routing and the stats command (no tracking gate, no database), its
/// <see cref="FakeChatPlatform"/> wired to the router. Built, never started.
/// </summary>
internal sealed class StatsCommandHost : IDisposable
{
    public static readonly PlatformRef Alice = TestPlatform.User("alice");

    private readonly IHost _host;

    private StatsCommandHost(IHost host)
    {
        _host = host;
        Platform = new FakeChatPlatform(Substitute.For<IPlatformEventDispatcher>(), TestTime.CreateProvider())
            .WithCommandRouter(host.Services.GetRequiredService<ICommandRouter>());
    }

    public IServiceProvider Services => _host.Services;

    public FakeChatPlatform Platform { get; }

    public FakeLogCollector Logs => _host.Services.GetFakeLogCollector();

    public static StatsCommandHost Create(string publicWebUrl)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{LinksOptions.SectionName}:PublicWebUrl"] = publicWebUrl,
        });
        builder.Services.AddFakeLogging();
        builder.Services.AddCommandRouting().AddStatsCommands();
        return new StatsCommandHost(builder.Build());
    }

    public Task<CommandReply> RunAsync(PlatformRef member) =>
        Platform.InvokeCommandAsync(member, "apollo stats", TestContext.Current.CancellationToken);

    public void Dispose() => _host.Dispose();
}
