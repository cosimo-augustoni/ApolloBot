using Apollo.Domain.Presence;
using Apollo.Testing;

namespace Apollo.Domain.Tests.Presence;

public sealed class PresenceSessionStateMachineTests
{
    private static readonly Guid _alice = Guid.NewGuid();
    private static readonly Guid _bob = Guid.NewGuid();

    private static readonly PresenceActivity _factorio = new(PresenceActivityType.Playing, "Factorio");
    private static readonly PresenceActivity _celeste = new(PresenceActivityType.Playing, "Celeste");
    private static readonly PresenceActivity _spotify = new(PresenceActivityType.Listening, "Spotify");

    private static readonly DateTimeOffset _startedAt = TestTime.Now;
    private static readonly DateTimeOffset _later = TestTime.Now.AddMinutes(5);

    private readonly PresenceSessionStateMachine _machine = new();

    [Fact]
    public void Update_WhenAnActivityAppears_ShouldOpenASession()
    {
        var changes = _machine.Update(_alice, [_factorio], _startedAt);

        changes.ShouldBe([new PresenceSessionOpened(_alice, _factorio, _startedAt)]);
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenAnActivityDisappears_ShouldCloseItsSessionWithItsSpan()
    {
        _machine.Update(_alice, [_factorio], _startedAt);

        var changes = _machine.Update(_alice, [], _later);

        changes.ShouldBe([new PresenceSessionClosed(_alice, _factorio, _startedAt, _later)]);
        _machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Update_WhenTheActivityStaysTheSame_ShouldChangeNothing()
    {
        _machine.Update(_alice, [_factorio], _startedAt);

        var changes = _machine.Update(_alice, [_factorio], _later);

        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WhenUserHasTwoActivities_ShouldTrackTwoIndependentSessions()
    {
        _machine.Update(_alice, [_factorio], _startedAt);
        var spotifyStartedAt = _startedAt.AddMinutes(2);
        _machine.Update(_alice, [_factorio, _spotify], spotifyStartedAt);

        var changes = _machine.Update(_alice, [_spotify], _later);

        changes.ShouldBe([new PresenceSessionClosed(_alice, _factorio, _startedAt, _later)]);
        _machine.StopAll(_later.AddMinutes(1)).ShouldBe([new PresenceSessionClosed(_alice, _spotify, spotifyStartedAt, _later.AddMinutes(1))]);
    }

    [Fact]
    public void Update_WhenUserSwitchesActivity_ShouldCloseTheOldSessionAndOpenTheNewOneAtTheSameInstant()
    {
        _machine.Update(_alice, [_factorio], _startedAt);

        var changes = _machine.Update(_alice, [_celeste], _later);

        changes.ShouldBe([
            new PresenceSessionClosed(_alice, _factorio, _startedAt, _later),
            new PresenceSessionOpened(_alice, _celeste, _later),
        ]);
    }

    [Fact]
    public void Update_WhenTheSameNameComesWithAnotherType_ShouldBeAnotherSession()
    {
        _machine.Update(_alice, [_factorio], _startedAt);
        var streamingFactorio = new PresenceActivity(PresenceActivityType.Streaming, "Factorio");

        var changes = _machine.Update(_alice, [streamingFactorio], _later);

        changes.ShouldBe([
            new PresenceSessionClosed(_alice, _factorio, _startedAt, _later),
            new PresenceSessionOpened(_alice, streamingFactorio, _later),
        ]);
    }

    [Fact]
    public void Update_WhenAnActivityIsReportedTwice_ShouldOpenOneSession()
    {
        var changes = _machine.Update(_alice, [_factorio, _factorio], _startedAt);

        changes.ShouldHaveSingleItem();
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenAnotherUserChanges_ShouldLeaveTheFirstUsersSessionsAlone()
    {
        _machine.Update(_alice, [_factorio], _startedAt);

        var changes = _machine.Update(_bob, [], _later);

        changes.ShouldBeEmpty();
        _machine.Users.ShouldBe([_alice]);
    }

    [Fact]
    public void Stop_ShouldCloseEveryOpenSessionOfTheUserAndForgetThem()
    {
        _machine.Update(_alice, [_factorio, _spotify], _startedAt);
        _machine.Update(_bob, [_celeste], _startedAt);

        var changes = _machine.Stop(_alice, _later);

        changes.ShouldBe([
            new PresenceSessionClosed(_alice, _factorio, _startedAt, _later),
            new PresenceSessionClosed(_alice, _spotify, _startedAt, _later),
        ]);
        _machine.Users.ShouldBe([_bob]);
    }

    [Fact]
    public void StopAll_ShouldCloseEverySessionAndForgetEveryone()
    {
        _machine.Update(_alice, [_factorio], _startedAt);
        _machine.Update(_bob, [_celeste], _startedAt);

        var changes = _machine.StopAll(_later);

        changes.ShouldBe(
            [new PresenceSessionClosed(_alice, _factorio, _startedAt, _later), new PresenceSessionClosed(_bob, _celeste, _startedAt, _later)],
            ignoreOrder: true);
        _machine.OpenSessionCount.ShouldBe(0);
        _machine.Users.ShouldBeEmpty();
    }

    [Fact]
    public void Clone_ShouldBeIndependentOfTheOriginal()
    {
        _machine.Update(_alice, [_factorio], _startedAt);

        var copy = _machine.Clone();
        copy.Update(_alice, [], _later);

        _machine.OpenSessionCount.ShouldBe(1);
        copy.OpenSessionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(300, true)]
    public void IsLongEnough_ShouldRequireAtLeastOneMinute(int seconds, bool expected)
    {
        var closed = new PresenceSessionClosed(_alice, _factorio, _startedAt, _startedAt.AddSeconds(seconds));

        closed.IsLongEnough.ShouldBe(expected);
    }
}
