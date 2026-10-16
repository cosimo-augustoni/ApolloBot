using Apollo.Platform.Abstractions.Commands;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging;

namespace Apollo.Bot.Tests.Commands;

/// <summary>
/// The router behind <see cref="ICommandRouter"/>, end to end through <see cref="FakeChatPlatform"/> with sample
/// handlers registered in DI.
/// </summary>
public sealed class CommandRouterTests : IDisposable
{
    private const string Error = "Das hat leider nicht geklappt. Bitte versuche es später noch einmal.";
    private const string NotForYou = "Dieser Button ist nicht für dich.";
    private const string Footer = "\n\nDatenschutz: https://apollo.example.test/datenschutz";

    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly Platform.Abstractions.PlatformRef _alice = TestPlatform.User("alice");
    private static readonly Platform.Abstractions.PlatformRef _bob = TestPlatform.User("bob");

    private readonly CommandTestHost _host = CommandTestHost.Create();

    private FakeChatPlatform Platform => _host.Platform;

    [Fact]
    public async Task HandleCommand_WhenHandlerIsRegisteredForPath_ShouldPassGuildMemberAndPath()
    {
        await Platform.InvokeCommandAsync(_alice, "apollo tracking status", _ct);

        var invocation = _host.Calls.Commands.ShouldHaveSingleItem();
        invocation.Guild.ShouldBe(TestPlatform.Guild);
        invocation.Member.ShouldBe(_alice);
        invocation.Path.ShouldBe("apollo tracking status");
        invocation.At.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task HandleCommand_WhenHandlerIsRegisteredForPath_ShouldSendItsReplyWithButtonsAndVisibility()
    {
        await Platform.InvokeCommandAsync(_alice, "apollo tracking status", _ct);

        var reply = Platform.Replies.ShouldHaveSingleItem().Reply;
        reply.Text.ShouldBe(TrackingStatusHandler.Text + Footer);
        reply.Buttons.ShouldHaveSingleItem().Id.ShouldBe("confirm:alice");
        reply.Visibility.ShouldBe(ReplyVisibility.Public);
    }

    [Fact]
    public async Task HandleCommand_WhenNoHandlerHasThePath_ShouldLogAndReplyWithGenericGermanError()
    {
        var reply = await Platform.InvokeCommandAsync(_alice, "apollo unknown", _ct);

        reply.ShouldBe(new CommandReply(Error + Footer, [], ReplyVisibility.Private));
        _host.Logs.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Warning && log.Message.Contains("apollo unknown"));
    }

    [Fact]
    public async Task HandleCommand_WhenHandlerThrows_ShouldLogErrorAndReplyWithPrivateGenericGermanError()
    {
        var reply = await Platform.InvokeCommandAsync(_alice, "apollo broken", _ct);

        reply.ShouldBe(new CommandReply(Error + Footer, [], ReplyVisibility.Private));
        _host.Logs.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Error
            && log.Exception is InvalidOperationException
            && log.Message.Contains("apollo broken"));
    }

    [Fact]
    public async Task HandleCommand_WhenCancelled_ShouldNotSwallowTheCancellation()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => Platform.InvokeCommandAsync(_alice, "apollo tracking status", new Dictionary<string, object?>(), cancelled.Token));
    }

    [Fact]
    public async Task HandleButton_WhenClickedByTheMemberItWasIssuedTo_ShouldPassTheButtonInvocationToItsHandler()
    {
        await Platform.ClickButtonAsync(_alice, "confirm:alice:yes", _ct);

        _host.Calls.Buttons.ShouldBe([new ButtonInvocation(TestPlatform.Guild, _alice, "confirm:alice:yes", TestTime.Now)]);
    }

    [Fact]
    public async Task HandleButton_WhenClickedByTheMemberItWasIssuedTo_ShouldSendTheHandlerReplyPrivately()
    {
        var reply = await Platform.ClickButtonAsync(_alice, "confirm:alice", _ct);

        reply.ShouldBe(new CommandReply(ConfirmButtonHandler.Text + Footer, [], ReplyVisibility.Private));
    }

    [Fact]
    public async Task HandleButton_WhenClickedByAnotherMember_ShouldRunNoHandlerAndReplyNotForYou()
    {
        var reply = await Platform.ClickButtonAsync(_bob, "confirm:alice", _ct);

        _host.Calls.Buttons.ShouldBeEmpty();
        reply.ShouldBe(new CommandReply(NotForYou + Footer, [], ReplyVisibility.Private));
    }

    [Theory]
    [InlineData("unknown:alice")]
    [InlineData("malformed")]
    public async Task HandleButton_WhenNoHandlerHasThePrefix_ShouldLogAndReplyWithGenericGermanError(string buttonId)
    {
        var reply = await Platform.ClickButtonAsync(_alice, buttonId, _ct);

        reply.ShouldBe(new CommandReply(Error + Footer, [], ReplyVisibility.Private));
        _host.Logs.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Warning && log.Message.Contains(buttonId));
    }

    [Fact]
    public async Task HandleButton_WhenHandlerThrows_ShouldLogErrorAndReplyWithPrivateGenericGermanError()
    {
        var reply = await Platform.ClickButtonAsync(_alice, "explode:alice", _ct);

        reply.ShouldBe(new CommandReply(Error + Footer, [], ReplyVisibility.Private));
        _host.Logs.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Error && log.Exception is InvalidOperationException);
    }

    [Theory]
    [InlineData("apollo tracking status")]
    [InlineData("apollo unknown")]
    [InlineData("apollo broken")]
    public async Task HandleCommand_EveryReply_ShouldEndWithThePrivacyPageLink(string path)
    {
        var reply = await Platform.InvokeCommandAsync(_alice, path, _ct);

        reply.Text.ShouldEndWith("https://apollo.example.test/datenschutz");
    }

    [Theory]
    [InlineData("confirm:alice")]
    [InlineData("confirm:bob")]
    [InlineData("unknown:alice")]
    [InlineData("explode:alice")]
    public async Task HandleButton_EveryReply_ShouldEndWithThePrivacyPageLink(string buttonId)
    {
        var reply = await Platform.ClickButtonAsync(_alice, buttonId, _ct);

        reply.Text.ShouldEndWith("https://apollo.example.test/datenschutz");
    }

    [Fact]
    public async Task HandleCommand_WhenPublicWebUrlEndsWithSlash_ShouldLinkThePrivacyPageWithoutDoubleSlash()
    {
        var settings = CommandTestHost.ValidSettings();
        settings["Apollo:Links:PublicWebUrl"] = "https://apollo.example.test/";
        using var host = CommandTestHost.Create(settings);

        var reply = await host.Platform.InvokeCommandAsync(_alice, "apollo tracking status", _ct);

        reply.Text.ShouldEndWith("Datenschutz: https://apollo.example.test/datenschutz");
    }

    public void Dispose() => _host.Dispose();
}
