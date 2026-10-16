using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Testing;
using Apollo.Testing.Platform;

namespace Apollo.Platform.Abstractions.Tests.Testing;

public sealed class FakeChatPlatformCommandTests
{
    private static readonly PlatformRef _alice = TestPlatform.User("alice");

    private readonly RecordingRouter _router = new();
    private readonly FakeChatPlatform _platform;

    public FakeChatPlatformCommandTests() =>
        _platform = new FakeChatPlatform(NSubstitute.Substitute.For<IPlatformEventDispatcher>(), TestTime.CreateProvider())
            .WithCommandRouter(_router);

    [Fact]
    public async Task InvokeCommandAsync_ShouldDeliverTheInvocationForTheGuildAtNow()
    {
        Dictionary<string, object?> options = new() { ["days"] = 7L };

        await _platform.InvokeCommandAsync(_alice, "apollo stats", options, TestContext.Current.CancellationToken);

        var invocation = _router.Commands.ShouldHaveSingleItem();
        invocation.ShouldBe(invocation with { Guild = TestPlatform.Guild, Member = _alice, Path = "apollo stats", At = TestTime.Now });
        invocation.Options.ShouldBe(options);
    }

    [Fact]
    public async Task InvokeCommandAsync_ShouldRecordAndReturnTheReply()
    {
        var reply = await _platform.InvokeCommandAsync(_alice, "apollo stats", TestContext.Current.CancellationToken);

        reply.Text.ShouldBe("command apollo stats");
        _platform.Replies.ShouldBe([new FakeReply(_alice, reply)]);
    }

    [Fact]
    public async Task ClickButtonAsync_ShouldDeliverTheButtonInvocationAndRecordTheReply()
    {
        var reply = await _platform.ClickButtonAsync(_alice, "confirm:alice", TestContext.Current.CancellationToken);

        _router.Buttons.ShouldBe([new ButtonInvocation(TestPlatform.Guild, _alice, "confirm:alice", TestTime.Now)]);
        _platform.Replies.ShouldBe([new FakeReply(_alice, reply)]);
    }

    [Fact]
    public async Task InvokeCommandAsync_WhenNoRouterIsSet_ShouldThrow()
    {
        var platform = new FakeChatPlatform(NSubstitute.Substitute.For<IPlatformEventDispatcher>(), TestTime.CreateProvider());

        await Should.ThrowAsync<InvalidOperationException>(
            () => platform.InvokeCommandAsync(_alice, "apollo stats", TestContext.Current.CancellationToken));
    }

    private sealed class RecordingRouter : ICommandRouter
    {
        public List<CommandInvocation> Commands { get; } = [];

        public List<ButtonInvocation> Buttons { get; } = [];

        public Task<CommandReply> HandleCommandAsync(CommandInvocation invocation, CancellationToken cancellationToken)
        {
            Commands.Add(invocation);
            return Task.FromResult(CommandReply.Private($"command {invocation.Path}"));
        }

        public Task<CommandReply> HandleButtonAsync(ButtonInvocation invocation, CancellationToken cancellationToken)
        {
            Buttons.Add(invocation);
            return Task.FromResult(CommandReply.Private($"button {invocation.ButtonId}"));
        }
    }
}
