using Apollo.Bot.Commands;
using Apollo.Bot.Links;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;

namespace Apollo.Bot.Tests.Commands;

/// <summary>
/// A host with the real command routing and the sample handlers, wired to a <see cref="FakeChatPlatform"/>. Built,
/// never started.
/// </summary>
internal sealed class CommandTestHost : IDisposable
{
    public const string PublicWebUrl = "https://apollo.example.test";

    private readonly IHost _host;

    private CommandTestHost(IHost host)
    {
        _host = host;
        Platform = new FakeChatPlatform(Substitute.For<IPlatformEventDispatcher>(), TestTime.CreateProvider())
            .WithCommandRouter(host.Services.GetRequiredService<ICommandRouter>());
    }

    public FakeChatPlatform Platform { get; }

    public ICommandRouter Router => _host.Services.GetRequiredService<ICommandRouter>();

    public HandlerCalls Calls => _host.Services.GetRequiredService<HandlerCalls>();

    public FakeLogCollector Logs => _host.Services.GetFakeLogCollector();

    public static Dictionary<string, string?> ValidSettings() => new()
    {
        [$"{LinksOptions.SectionName}:PublicWebUrl"] = PublicWebUrl,
    };

    /// <summary>
    /// The sample commands, as the definitions their handlers need.
    /// </summary>
    public static readonly CommandDefinition Apollo = new("apollo", "Apollo")
    {
        Groups = [new SubcommandGroupDefinition("tracking", "Erfassung", [new("status", "Status")])],
        Subcommands = [new SubcommandDefinition("broken", "Kaputt")],
    };

    public static IHost Build(IDictionary<string, string?> settings, Action<IServiceCollection>? configure = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddFakeLogging();
        builder.Services.AddSingleton<HandlerCalls>();
        builder.Services.AddCommandRouting()
            .AddCommandHandler<TrackingStatusHandler>()
            .AddCommandHandler<ThrowingCommandHandler>()
            .AddButtonHandler<ConfirmButtonHandler>()
            .AddButtonHandler<ThrowingButtonHandler>()
            .AddCommandDefinition(Apollo);
        configure?.Invoke(builder.Services);
        return builder.Build();
    }

    public static CommandTestHost Create(IDictionary<string, string?>? settings = null) => new(Build(settings ?? ValidSettings()));

    public void Dispose() => _host.Dispose();
}
