using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing.Diagnostics;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCord;
using NetCord.Rest;
using ButtonStyle = Apollo.Platform.Abstractions.Commands.ButtonStyle;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordInteractionHandlerTests : IDisposable
{
    private const ulong ForeignGuildId = 222222222222222222;

    private static readonly PlatformRef _guild = new(PlatformKind.Discord, "111111111111111111");

    private readonly NetCordInteractions _interactions = new();
    private readonly StubCommandRouter _router = new();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly FakeTimeProvider _time;
    private readonly DiscordInteractionHandler _handler;

    public DiscordInteractionHandlerTests()
    {
        // Discord's deadline counts from the interaction's creation (its snowflake), so the clock starts there.
        _time = new FakeTimeProvider(TrackingStatus(DiscordHost.GuildId).CreatedAt);
        var mapper = new DiscordEventMapper(
            Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId }),
            new RecordingEventDispatcher(),
            new(_meterFactory),
            new FakeLogger<DiscordEventMapper>());
        _handler = new DiscordInteractionHandler(mapper, _router, _time, new FakeLogger<DiscordInteractionHandler>());
    }

    [Fact]
    public async Task HandleAsync_WhenSlashCommandIsFromConfiguredGuild_ShouldDeliverTheInvocationToTheRouter()
    {
        await _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId));

        var invocation = _router.Commands.ShouldHaveSingleItem();
        invocation.Guild.ShouldBe(_guild);
        invocation.Member.ShouldBe(new PlatformRef(PlatformKind.Discord, "42"));
        invocation.Path.ShouldBe("apollo tracking status");
    }

    [Fact]
    public async Task HandleAsync_WhenRouterRepliesInTime_ShouldSendTheReplyAsEphemeralMessage()
    {
        _router.Reply = CommandReply.Private("Du wirst erfasst.");

        await _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId));

        var message = MessageResponse(_interactions.Responses.ShouldHaveSingleItem());
        message.Content.ShouldBe("Du wirst erfasst.");
        message.Flags.ShouldNotBeNull().HasFlag(MessageFlags.Ephemeral).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenButtonIsClicked_ShouldDeliverTheButtonInvocationAndReplyEphemerally()
    {
        _router.Reply = CommandReply.Private("Gelöscht.");

        await _handler.HandleAsync(_interactions.ButtonClick(DiscordHost.GuildId, userId: 42, "privacy-delete:42"));

        _router.Buttons.ShouldHaveSingleItem().ShouldBe(_router.Buttons[0] with
        {
            Guild = _guild,
            Member = new PlatformRef(PlatformKind.Discord, "42"),
            ButtonId = "privacy-delete:42",
        });
        var message = MessageResponse(_interactions.Responses.ShouldHaveSingleItem());
        message.Content.ShouldBe("Gelöscht.");
        message.Flags.ShouldNotBeNull().HasFlag(MessageFlags.Ephemeral).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenReplyHasButtons_ShouldSendThemAsDiscordButtons()
    {
        _router.Reply = new CommandReply("Wirklich?", [new ReplyButton("privacy-delete:42", "Löschen", ButtonStyle.Danger)]);

        await _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId));

        var button = MessageResponse(_interactions.Responses.ShouldHaveSingleItem()).Components.ShouldNotBeNull()
            .Cast<ActionRowProperties>().ShouldHaveSingleItem().Components.Cast<ButtonProperties>().ShouldHaveSingleItem();
        button.CustomId.ShouldBe("privacy-delete:42");
        button.Label.ShouldBe("Löschen");
    }

    [Theory]
    [InlineData(ForeignGuildId)]
    [InlineData(null)]
    public async Task HandleAsync_WhenSlashCommandIsFromAnotherGuildOrADm_ShouldNotReachTheRouterOrRespond(ulong? guildId)
    {
        await _handler.HandleAsync(TrackingStatus(guildId));

        _router.Commands.ShouldBeEmpty();
        _interactions.Responses.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ForeignGuildId)]
    [InlineData(null)]
    public async Task HandleAsync_WhenButtonIsFromAnotherGuildOrADm_ShouldNotReachTheRouterOrRespond(ulong? guildId)
    {
        await _handler.HandleAsync(_interactions.ButtonClick(guildId, userId: 42, "privacy-delete:42"));

        _router.Buttons.ShouldBeEmpty();
        _interactions.Responses.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenRouterTakesLongerThanTheDeferralLimit_ShouldDeferEphemerallyThenEditTheResponse()
    {
        _router.Hold = true;
        _router.Reply = CommandReply.Private("Fertig.");
        _interactions.Rest.Respond = _ => NetCordInteractions.ResponseMessageJson;

        var handling = _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId)).AsTask();
        await _router.Called.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _time.Advance(DiscordInteractionHandler.DeferAfter);
        await WaitUntilAsync(() => _interactions.Responses.Count == 1);
        _router.Release();
        await handling;

        var deferred = _interactions.Responses.ShouldHaveSingleItem().ShouldBeOfType<InteractionCallbackProperties<InteractionMessageProperties>>();
        deferred.Type.ShouldBe(InteractionCallbackType.DeferredMessage);
        deferred.Data.Flags.ShouldBe(MessageFlags.Ephemeral);
        var edit = _interactions.Rest.Requests.ShouldHaveSingleItem();
        edit.Method.ShouldBe(HttpMethod.Patch);
        edit.Path.ShouldEndWith($"/webhooks/{NetCordInteractions.ApplicationId}/{NetCordInteractions.Token}/messages/@original");
        _interactions.Rest.Bodies.ShouldHaveSingleItem().ShouldNotBeNull().ShouldContain("Fertig.");
    }

    [Fact]
    public async Task HandleAsync_WhenRouterRepliesInTime_ShouldNotDefer()
    {
        await _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId));
        _time.Advance(DiscordInteractionHandler.DeferAfter);

        _interactions.Responses.ShouldHaveSingleItem().Type.ShouldBe(InteractionCallbackType.Message);
        _interactions.Rest.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenTheInteractionArrivesLate_ShouldDeferTwoSecondsAfterItWasCreated()
    {
        _router.Hold = true;
        _interactions.Rest.Respond = _ => NetCordInteractions.ResponseMessageJson;
        _time.Advance(TimeSpan.FromSeconds(1.5));

        var handling = _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId)).AsTask();
        await _router.Called.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _interactions.Responses.ShouldBeEmpty();
        _time.Advance(TimeSpan.FromSeconds(0.5));
        await WaitUntilAsync(() => _interactions.Responses.Count == 1);
        _router.Release();
        await handling;

        _interactions.Responses.ShouldHaveSingleItem().Type.ShouldBe(InteractionCallbackType.DeferredMessage);
    }

    [Fact]
    public async Task HandleAsync_WhenTheInteractionIsAlreadyPastTheDeferralLimit_ShouldDeferAtOnce()
    {
        _router.Hold = true;
        _interactions.Rest.Respond = _ => NetCordInteractions.ResponseMessageJson;
        _time.Advance(TimeSpan.FromSeconds(3));

        var handling = _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId)).AsTask();
        await WaitUntilAsync(() => _interactions.Responses.Count == 1);
        _router.Release();
        await handling;

        _interactions.Responses.ShouldHaveSingleItem().Type.ShouldBe(InteractionCallbackType.DeferredMessage);
    }

    [Fact]
    public async Task HandleAsync_WhenTheInvocationCannotBeMapped_ShouldReplyWithTheErrorReplyEphemerally()
    {
        var interaction = _interactions.SlashCommand(DiscordHost.GuildId, userId: 42, "apollo", """
            [ { "name": "stats", "type": 1, "options": [ { "name": "days", "type": 4, "value": "not a number" } ] } ]
            """);

        await _handler.HandleAsync(interaction);

        _router.Commands.ShouldBeEmpty();
        var message = MessageResponse(_interactions.Responses.ShouldHaveSingleItem());
        message.Content.ShouldBe(StubCommandRouter.ErrorText);
        message.Flags.ShouldNotBeNull().HasFlag(MessageFlags.Ephemeral).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_WhenTheRouterFailsInTime_ShouldReplyWithTheErrorReply()
    {
        _router.Failure = new InvalidOperationException("The router failed.");

        await _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId));

        MessageResponse(_interactions.Responses.ShouldHaveSingleItem()).Content.ShouldBe(StubCommandRouter.ErrorText);
    }

    [Fact]
    public async Task HandleAsync_WhenTheRouterFailsAfterDeferral_ShouldEditTheResponseWithTheErrorReply()
    {
        _router.Hold = true;
        _router.Failure = new InvalidOperationException("The router failed.");
        _interactions.Rest.Respond = _ => NetCordInteractions.ResponseMessageJson;

        var handling = _handler.HandleAsync(TrackingStatus(DiscordHost.GuildId)).AsTask();
        await _router.Called.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _time.Advance(DiscordInteractionHandler.DeferAfter);
        await WaitUntilAsync(() => _interactions.Responses.Count == 1);
        _router.Release();
        await handling;

        _interactions.Responses.ShouldHaveSingleItem().Type.ShouldBe(InteractionCallbackType.DeferredMessage);
        _interactions.Rest.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Patch);
        _interactions.Rest.Bodies.ShouldHaveSingleItem().ShouldNotBeNull().ShouldContain(StubCommandRouter.ErrorText);
    }

    public void Dispose()
    {
        _interactions.Dispose();
        _meterFactory.Dispose();
    }

    private static InteractionMessageProperties MessageResponse(InteractionCallbackProperties callback)
    {
        callback.Type.ShouldBe(InteractionCallbackType.Message);
        return callback.ShouldBeOfType<InteractionCallbackProperties<InteractionMessageProperties>>().Data;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private SlashCommandInteraction TrackingStatus(ulong? guildId) =>
        _interactions.SlashCommand(guildId, userId: 42, "apollo", """
            [ { "name": "tracking", "type": 2, "options": [ { "name": "status", "type": 1 } ] } ]
            """);
}
