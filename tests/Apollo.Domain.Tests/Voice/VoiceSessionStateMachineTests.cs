using Apollo.Domain.Voice;
using Apollo.Testing;

namespace Apollo.Domain.Tests.Voice;

public sealed class VoiceSessionStateMachineTests
{
    private static readonly Guid _alice = Guid.NewGuid();
    private static readonly Guid _bob = Guid.NewGuid();
    private static readonly Guid _lounge = Guid.NewGuid();
    private static readonly Guid _gaming = Guid.NewGuid();

    private static readonly DateTimeOffset _joinedAt = TestTime.Now;
    private static readonly DateTimeOffset _later = TestTime.Now.AddMinutes(5);

    private static readonly VoiceFlags _muted = VoiceFlags.None with { SelfMute = true };

    private readonly VoiceSessionStateMachine _machine = new();

    [Fact]
    public void Update_WhenUserJoins_ShouldOpenASessionWithTheirFlags()
    {
        var changes = _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        changes.ShouldBe([new VoiceSessionOpened(_alice, _joinedAt, _lounge, _muted)]);
    }

    [Fact]
    public void Update_WhenUserLeaves_ShouldCloseTheSessionAsLeft()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Update(_alice, Out(), _later);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void Update_WhenUserMoves_ShouldCloseTheOldSessionAsMovedAndOpenANewOneAtTheSameInstant()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        var changes = _machine.Update(_alice, In(_gaming, _muted), _later);

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Moved),
            new VoiceSessionOpened(_alice, _later, _gaming, _muted),
        ]);
    }

    public static TheoryData<string> FlagChanges => ["mute", "deaf", "stream", "video"];

    [Theory]
    [MemberData(nameof(FlagChanges))]
    public void Update_WhenAFlagChanges_ShouldStartANewSegmentAndKeepTheSession(string flag)
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        var flags = flag switch
        {
            "mute" => VoiceFlags.None with { SelfMute = true },
            "deaf" => VoiceFlags.None with { SelfDeaf = true },
            "stream" => VoiceFlags.None with { Streaming = true },
            _ => VoiceFlags.None with { Video = true },
        };

        var changes = _machine.Update(_alice, In(_lounge, flags), _later);

        changes.ShouldBe([new VoiceSegmentChanged(_alice, _later, flags)]);
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenNothingRelevantChanged_ShouldChangeNothing()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        _machine.Update(_alice, In(_lounge, _muted), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Update_WhenAUserNotInVoiceLeaves_ShouldChangeNothing()
    {
        _machine.Update(_alice, Out(), _joinedAt).ShouldBeEmpty();
    }

    [Fact]
    public void OpenSessionCount_ShouldCountUsersInVoice()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        _machine.Update(_bob, In(_gaming), _joinedAt);
        _machine.Update(_bob, Out(), _later);

        _machine.OpenSessionCount.ShouldBe(1);
        _machine.UsersInVoice.ShouldBe([_alice]);
    }

    [Fact]
    public void Update_WhenASecondConnectionJoins_ShouldKeepTheSessionOfTheFirst()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);

        _machine.Update(_alice, In(_gaming, _muted, connectionId: "b"), _later).ShouldBeEmpty();
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenOneOfTwoConnectionsInTheSameChannelLeaves_ShouldKeepTheSession()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_lounge, connectionId: "b"), _joinedAt);

        _machine.Update(_alice, Out(connectionId: "a"), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Update_WhenTheFirstConnectionLeavesAndTheOtherIsInAnotherChannel_ShouldMoveToIt()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_gaming, _muted, connectionId: "b"), _joinedAt);

        var changes = _machine.Update(_alice, Out(connectionId: "a"), _later);

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Moved),
            new VoiceSessionOpened(_alice, _later, _gaming, _muted),
        ]);
    }

    [Fact]
    public void Update_WhenTheLastConnectionLeaves_ShouldCloseTheSessionAsLeft()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_lounge, connectionId: "b"), _joinedAt);
        _machine.Update(_alice, Out(connectionId: "a"), _later);

        var changes = _machine.Update(_alice, Out(connectionId: "b"), _later);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void Update_WhenTheSecondConnectionChangesFlags_ShouldChangeNothing()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_lounge, connectionId: "b"), _joinedAt);

        _machine.Update(_alice, In(_lounge, _muted, connectionId: "b"), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_WhenUserIsInVoice_ShouldOpenASession()
    {
        var changes = _machine.Reconcile(_alice, [In(_lounge, _muted)], _joinedAt, VoiceSessionEndReason.BotRestart);

        changes.ShouldBe([new VoiceSessionOpened(_alice, _joinedAt, _lounge, _muted)]);
    }

    [Fact]
    public void Reconcile_WhenTheSessionMatches_ShouldChangeNothing()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        _machine.Reconcile(_alice, [In(_lounge, _muted)], _later, VoiceSessionEndReason.BotRestart).ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_WhenUserIsNoLongerInVoice_ShouldCloseTheSessionWithTheGivenReason()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Reconcile(_alice, [], _later, VoiceSessionEndReason.BotRestart);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.BotRestart)]);
        _machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Reconcile_WhenUserIsInAnotherChannel_ShouldCloseWithTheGivenReasonAndOpenANewSession()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Reconcile(_alice, [In(_gaming)], _later, VoiceSessionEndReason.BotRestart);

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.BotRestart),
            new VoiceSessionOpened(_alice, _later, _gaming, VoiceFlags.None),
        ]);
    }

    [Fact]
    public void Reconcile_ShouldReplaceAllConnectionsOfTheUser()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Reconcile(_alice, [In(_lounge, connectionId: "b")], _later, VoiceSessionEndReason.BotRestart);

        var changes = _machine.Update(_alice, Out(connectionId: "b"), _later);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void Stop_WhenUserIsInVoice_ShouldCloseTheSessionWithTheReasonAndForgetTheUser()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Stop(_alice, _later, VoiceSessionEndReason.TrackingStopped);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.TrackingStopped)]);
        _machine.Update(_alice, Out(), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Stop_WhenUserIsNotInVoice_ShouldChangeNothing()
    {
        _machine.Stop(_alice, _later, VoiceSessionEndReason.TrackingStopped).ShouldBeEmpty();
    }

    [Fact]
    public void StopAll_ShouldCloseEverySessionAndForgetEveryone()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        _machine.Update(_bob, In(_gaming), _joinedAt);

        var changes = _machine.StopAll(_later, VoiceSessionEndReason.BotRestart);

        changes.ShouldBe(
            [
                new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.BotRestart),
                new VoiceSessionClosed(_bob, _later, VoiceSessionEndReason.BotRestart),
            ],
            ignoreOrder: true);
        _machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Clone_ShouldCopyTheStateWithoutSharingIt()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);

        var copy = _machine.Clone();
        var copyChanges = copy.Update(_alice, Out(connectionId: "a"), _later);
        copy.Update(_bob, In(_gaming), _later);

        copyChanges.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
        copy.UsersInVoice.ShouldBe([_bob]);
        _machine.UsersInVoice.ShouldBe([_alice]);
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _later).ShouldBeEmpty();
    }

    private static VoiceConnection In(Guid channelId, VoiceFlags? flags = null, string? connectionId = null) =>
        new(connectionId, channelId, flags ?? VoiceFlags.None);

    private static VoiceConnection Out(string? connectionId = null) => new(connectionId, ChannelId: null, VoiceFlags.None);
}
