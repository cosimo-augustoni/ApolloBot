using Apollo.Bot.Commands;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apollo.Bot.Tests.Commands;

/// <summary>
/// Command definitions and handlers are checked when the bot starts, before the gateway connects.
/// </summary>
public sealed class CommandCatalogValidatorTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_WhenDefinitionsAndHandlersMatch_ShouldStart()
    {
        using var host = CommandTestHost.Build(CommandTestHost.ValidSettings());

        await host.StartAsync(_ct);

        await host.StopAsync(_ct);
    }

    [Fact]
    public async Task Start_WhenTwoHandlersHaveTheSamePath_ShouldFailNamingThePath()
    {
        using var host = Build(services => services.AddCommandHandler<SecondTrackingStatusHandler>());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(_ct));

        exception.Message.ShouldContain("apollo tracking status");
    }

    [Fact]
    public async Task Start_WhenTwoButtonHandlersHaveTheSamePrefix_ShouldFailNamingThePrefix()
    {
        using var host = Build(services => services.AddButtonHandler<SecondConfirmButtonHandler>());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(_ct));

        exception.Message.ShouldContain(ConfirmButtonHandler.Prefix);
    }

    [Fact]
    public async Task Start_WhenTwoDefinitionsDeclareTheSamePath_ShouldFailNamingThePath()
    {
        using var host = Build(services => services.AddCommandDefinition(new CommandDefinition("apollo", "Apollo")
        {
            Subcommands = [new SubcommandDefinition("broken", "Nochmals")],
        }));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(_ct));

        exception.Message.ShouldContain("apollo broken");
    }

    [Fact]
    public async Task Start_WhenAHandlerPathIsNotDefined_ShouldFailNamingThePath()
    {
        using var host = Build(services => services.AddCommandHandler<UndefinedCommandHandler>());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => host.StartAsync(_ct));

        exception.Message.ShouldContain(UndefinedCommandHandler.CommandPath);
    }

    [Fact]
    public async Task Start_WhenADefinedPathHasNoHandler_ShouldWarnAndStart()
    {
        using var host = Build(services => services.AddCommandDefinition(new CommandDefinition("ping", "Ping")));

        await host.StartAsync(_ct);

        host.Services.GetFakeLogCollector().GetSnapshot()
            .ShouldContain(log => log.Level == LogLevel.Warning && log.Message.Contains("ping"));
        await host.StopAsync(_ct);
    }

    private static IHost Build(Action<IServiceCollection> configure) =>
        CommandTestHost.Build(CommandTestHost.ValidSettings(), configure);

    private sealed class SecondTrackingStatusHandler : ICommandHandler
    {
        public string Path => "apollo tracking status";

        public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken) =>
            Task.FromResult(CommandReply.Private("Zweiter"));
    }

    private sealed class SecondConfirmButtonHandler : IButtonHandler
    {
        public string IdPrefix => ConfirmButtonHandler.Prefix;

        public Task<CommandReply> HandleAsync(ButtonInvocation invocation, CancellationToken cancellationToken) =>
            Task.FromResult(CommandReply.Private("Zweiter"));
    }

    private sealed class UndefinedCommandHandler : ICommandHandler
    {
        public const string CommandPath = "apollo undefined";

        public string Path => CommandPath;

        public Task<CommandReply> HandleAsync(CommandInvocation invocation, CancellationToken cancellationToken) =>
            Task.FromResult(CommandReply.Private("Nie"));
    }
}
