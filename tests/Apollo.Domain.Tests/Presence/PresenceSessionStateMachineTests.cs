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

    private static readonly PresenceActivityDetails _allFields = new("d", "s", "l", "m", "p", 1, 2, "app");

    private readonly PresenceSessionStateMachine _machine = new();

    [Fact]
    public void Update_WhenAnActivityAppears_ShouldOpenASession()
    {
        var changes = _machine.Update(_alice, Seen(_factorio), _startedAt);

        changes.ShouldBe([new PresenceSessionOpened(_alice, _factorio, _startedAt)]);
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenAnActivityDisappears_ShouldCloseItsSessionWithItsSpan()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        var changes = _machine.Update(_alice, Seen(), _later);

        changes.ShouldBe([Closed(_alice, _factorio, _startedAt, _later)]);
        _machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Update_WhenTheActivityStaysTheSame_ShouldChangeNothing()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        var changes = _machine.Update(_alice, Seen(_factorio), _later);

        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Update_WhenUserHasTwoActivities_ShouldTrackTwoIndependentSessions()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);
        var spotifyStartedAt = _startedAt.AddMinutes(2);
        _machine.Update(_alice, Seen(_factorio, _spotify), spotifyStartedAt);

        var changes = _machine.Update(_alice, Seen(_spotify), _later);

        changes.ShouldBe([Closed(_alice, _factorio, _startedAt, _later)]);
        _machine.StopAll(_later.AddMinutes(1)).ShouldBe([Closed(_alice, _spotify, spotifyStartedAt, _later.AddMinutes(1))]);
    }

    [Fact]
    public void Update_WhenUserSwitchesActivity_ShouldCloseTheOldSessionAndOpenTheNewOneAtTheSameInstant()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        var changes = _machine.Update(_alice, Seen(_celeste), _later);

        changes.ShouldBe([
            Closed(_alice, _factorio, _startedAt, _later),
            new PresenceSessionOpened(_alice, _celeste, _later),
        ]);
    }

    [Fact]
    public void Update_WhenTheSameNameComesWithAnotherType_ShouldBeAnotherSession()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);
        var streamingFactorio = new PresenceActivity(PresenceActivityType.Streaming, "Factorio");

        var changes = _machine.Update(_alice, Seen(streamingFactorio), _later);

        changes.ShouldBe([
            Closed(_alice, _factorio, _startedAt, _later),
            new PresenceSessionOpened(_alice, streamingFactorio, _later),
        ]);
    }

    [Fact]
    public void Update_WhenAnActivityIsReportedTwice_ShouldOpenOneSession()
    {
        var changes = _machine.Update(_alice, Seen(_factorio, _factorio), _startedAt);

        changes.ShouldHaveSingleItem();
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenAnotherUserChanges_ShouldLeaveTheFirstUsersSessionsAlone()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        var changes = _machine.Update(_bob, Seen(), _later);

        changes.ShouldBeEmpty();
        _machine.Users.ShouldBe([_alice]);
    }

    [Fact]
    public void Stop_ShouldCloseEveryOpenSessionOfTheUserAndForgetThem()
    {
        _machine.Update(_alice, Seen(_factorio, _spotify), _startedAt);
        _machine.Update(_bob, Seen(_celeste), _startedAt);

        var changes = _machine.Stop(_alice, _later);

        changes.ShouldBe([
            Closed(_alice, _factorio, _startedAt, _later),
            Closed(_alice, _spotify, _startedAt, _later),
        ]);
        _machine.Users.ShouldBe([_bob]);
    }

    [Fact]
    public void StopAll_ShouldCloseEverySessionAndForgetEveryone()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);
        _machine.Update(_bob, Seen(_celeste), _startedAt);

        var changes = _machine.StopAll(_later);

        changes.ShouldBe(
            [Closed(_alice, _factorio, _startedAt, _later), Closed(_bob, _celeste, _startedAt, _later)],
            ignoreOrder: true);
        _machine.OpenSessionCount.ShouldBe(0);
        _machine.Users.ShouldBeEmpty();
    }

    [Fact]
    public void Clone_ShouldBeIndependentOfTheOriginal()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        var copy = _machine.Clone();
        copy.Update(_alice, Seen(), _later);

        _machine.OpenSessionCount.ShouldBe(1);
        copy.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Reconcile_WhenAnActivityIsGone_ShouldCloseItAtLastKnown()
    {
        _machine.Update(_alice, Seen(_factorio, _spotify), _startedAt);
        var lastKnownAt = _later;

        var changes = _machine.Reconcile(_alice, Seen(_spotify), lastKnownAt, now: _later.AddMinutes(5));

        changes.ShouldBe([Closed(_alice, _factorio, _startedAt, lastKnownAt)]);
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Reconcile_WhenTheSessionStartedAfterLastKnown_ShouldNotCloseItBeforeItsStart()
    {
        _machine.Update(_alice, Seen(_factorio), _later);

        var changes = _machine.Reconcile(_alice, Seen(), lastKnownAt: _startedAt, now: _later.AddMinutes(5));

        changes.ShouldBe([Closed(_alice, _factorio, _later, _later)]);
    }

    [Fact]
    public void Reconcile_WhenAnActivityHasNoSession_ShouldOpenOneNow()
    {
        var now = _later.AddMinutes(5);

        var changes = _machine.Reconcile(_alice, Seen(_celeste), lastKnownAt: _later, now);

        changes.ShouldBe([new PresenceSessionOpened(_alice, _celeste, now)]);
    }

    [Fact]
    public void Reconcile_WhenEverythingMatches_ShouldChangeNothingAndKeepTheStart()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        _machine.Reconcile(_alice, Seen(_factorio), lastKnownAt: _later, now: _later.AddMinutes(5)).ShouldBeEmpty();
        _machine.Update(_alice, Seen(), _later.AddMinutes(10)).ShouldBe([Closed(_alice, _factorio, _startedAt, _later.AddMinutes(10))]);
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(300, true)]
    public void IsLongEnough_ShouldRequireAtLeastOneMinute(int seconds, bool expected)
    {
        var closed = Closed(_alice, _factorio, _startedAt, _startedAt.AddSeconds(seconds));

        closed.IsLongEnough.ShouldBe(expected);
    }

    [Fact]
    public void Update_WhenTheSongChangesTwice_ShouldKeepOneSessionWithThreeSegmentsWithoutGaps()
    {
        var song1 = Song("Song 1", "Artist A", "Album X");
        var song2 = Song("Song 2", "Artist A; Artist B", "Album X");
        var song3 = Song("Song 3", "Artist C", "Album Y");
        var second = _startedAt.AddMinutes(2);
        var third = _startedAt.AddMinutes(4);

        _machine.Update(_alice, [new(_spotify, song1)], _startedAt);
        _machine.Update(_alice, [new(_spotify, song2)], second).ShouldBeEmpty();
        _machine.Update(_alice, [new(_spotify, song3)], third).ShouldBeEmpty();
        var changes = _machine.Update(_alice, Seen(), _later);

        changes.ShouldBe([
            new PresenceSessionClosed(_alice, _spotify, _startedAt, _later, [
                new PresenceSegment(_startedAt, second, song1),
                new PresenceSegment(second, third, song2),
                new PresenceSegment(third, _later, song3),
            ]),
        ]);
    }

    [Fact]
    public void Update_WhenOnlyTheReportedTimestampsChange_ShouldNotStartASegment()
    {
        var song = Song("Song 1", "Artist A", "Album X") with { ReportedStart = _startedAt, ReportedEnd = _startedAt.AddMinutes(3) };
        _machine.Update(_alice, [new(_spotify, song)], _startedAt);

        var seeked = song with { ReportedStart = _startedAt.AddSeconds(-40), ReportedEnd = _startedAt.AddSeconds(140) };
        _machine.Update(_alice, [new(_spotify, seeked)], _startedAt.AddMinutes(1));
        var changes = _machine.Update(_alice, Seen(), _later);

        changes.ShouldBe([new PresenceSessionClosed(_alice, _spotify, _startedAt, _later, [new PresenceSegment(_startedAt, _later, song)])]);
    }

    [Fact]
    public void Update_WhenTheGameStateAndThePartyChange_ShouldStartASegmentForEachChange()
    {
        var queue = new PresenceActivityDetails(Details: "Ranked", State: "In Queue", PartyId: "party-1", PartySize: 2, PartyMax: 5);
        var match = queue with { State = "In Match" };
        var grown = match with { PartySize = 3 };
        var inMatch = _startedAt.AddMinutes(2);
        var joined = _startedAt.AddMinutes(4);

        _machine.Update(_alice, [new(_factorio, queue)], _startedAt);
        _machine.Update(_alice, [new(_factorio, match)], inMatch);
        _machine.Update(_alice, [new(_factorio, grown)], joined);
        var changes = _machine.Update(_alice, Seen(), _later);

        changes.ShouldHaveSingleItem().ShouldBeOfType<PresenceSessionClosed>().Segments.ShouldBe([
            new PresenceSegment(_startedAt, inMatch, queue),
            new PresenceSegment(inMatch, joined, match),
            new PresenceSegment(joined, _later, grown),
        ]);
    }

    [Theory]
    [MemberData(nameof(SegmentFields))]
    public void Update_WhenOneSegmentFieldChanges_ShouldStartASegment(PresenceActivityDetails changed)
    {
        _machine.Update(_alice, [new(_factorio, _allFields)], _startedAt);

        _machine.Update(_alice, [new(_factorio, changed)], _startedAt.AddMinutes(1));

        _machine.StopAll(_later).ShouldHaveSingleItem().ShouldBeOfType<PresenceSessionClosed>().Segments.Count.ShouldBe(2);
    }

    public static TheoryData<PresenceActivityDetails> SegmentFields() =>
    [
        _allFields with { Details = "x" },
        _allFields with { State = "x" },
        _allFields with { LargeText = "x" },
        _allFields with { SmallText = "x" },
        _allFields with { PartyId = "x" },
        _allFields with { PartySize = 9 },
        _allFields with { PartyMax = 9 },
    ];

    [Fact]
    public void Update_WhenAnActivitySendsNoRichFields_ShouldCloseWithOneSegmentWithoutDetails()
    {
        _machine.Update(_alice, Seen(_factorio), _startedAt);

        var changes = _machine.Update(_alice, Seen(), _later);

        changes.ShouldBe([
            new PresenceSessionClosed(_alice, _factorio, _startedAt, _later, [new PresenceSegment(_startedAt, _later, PresenceActivityDetails.None)]),
        ]);
    }

    [Fact]
    public void Update_WhenTwoActivitiesRunAtOnce_ShouldGiveEachSessionItsOwnSegments()
    {
        var song = Song("Song 1", "Artist A", "Album X");
        var match = new PresenceActivityDetails(State: "In Match");
        _machine.Update(_alice, [new(_factorio, match), new(_spotify, song)], _startedAt);
        var nextSong = song with { Details = "Song 2" };
        var songChanged = _startedAt.AddMinutes(3);
        _machine.Update(_alice, [new(_factorio, match), new(_spotify, nextSong)], songChanged);

        var changes = _machine.StopAll(_later);

        changes.ShouldBe(
            [
                new PresenceSessionClosed(_alice, _factorio, _startedAt, _later, [new PresenceSegment(_startedAt, _later, match)]),
                new PresenceSessionClosed(_alice, _spotify, _startedAt, _later, [
                    new PresenceSegment(_startedAt, songChanged, song),
                    new PresenceSegment(songChanged, _later, nextSong),
                ]),
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Stop_ShouldCloseTheOpenSegmentWithItsSession()
    {
        var song = Song("Song 1", "Artist A", "Album X");
        var songChanged = _startedAt.AddMinutes(3);
        _machine.Update(_alice, [new(_spotify, Song("Song 0", "Artist A", "Album X"))], _startedAt);
        _machine.Update(_alice, [new(_spotify, song)], songChanged);

        var changes = _machine.Stop(_alice, _later);

        changes.ShouldHaveSingleItem().ShouldBeOfType<PresenceSessionClosed>().Segments[^1].ShouldBe(new PresenceSegment(songChanged, _later, song));
    }

    [Fact]
    public void Reconcile_WhenTheRichFieldsDiffer_ShouldEndTheSegmentAndStartANewOneAtTheReconciliation()
    {
        var queue = new PresenceActivityDetails(State: "In Queue");
        var match = new PresenceActivityDetails(State: "In Match");
        _machine.Update(_alice, [new(_factorio, queue)], _startedAt);
        var now = _later.AddMinutes(5);

        _machine.Reconcile(_alice, [new(_factorio, match)], lastKnownAt: _later, now).ShouldBeEmpty();

        _machine.StopAll(now.AddMinutes(1)).ShouldBe([
            new PresenceSessionClosed(_alice, _factorio, _startedAt, now.AddMinutes(1), [
                new PresenceSegment(_startedAt, now, queue),
                new PresenceSegment(now, now.AddMinutes(1), match),
            ]),
        ]);
    }

    [Fact]
    public void Reconcile_WhenAGoneSessionChangedAfterLastKnown_ShouldEndItsSegmentsAtTheClose()
    {
        var first = new PresenceActivityDetails(State: "In Queue");
        var second = new PresenceActivityDetails(State: "In Match");
        _machine.Update(_alice, [new(_factorio, first)], _startedAt);
        _machine.Update(_alice, [new(_factorio, second)], _later.AddMinutes(2));

        var changes = _machine.Reconcile(_alice, Seen(), lastKnownAt: _later, now: _later.AddMinutes(5));

        changes.ShouldBe([new PresenceSessionClosed(_alice, _factorio, _startedAt, _later, [new PresenceSegment(_startedAt, _later, first)])]);
    }

    [Fact]
    public void Clone_ShouldKeepTheSegmentsOfTheOriginal()
    {
        _machine.Update(_alice, [new(_spotify, Song("Song 1", "A", "X"))], _startedAt);
        var copy = _machine.Clone();

        copy.Update(_alice, [new(_spotify, Song("Song 2", "A", "X"))], _startedAt.AddMinutes(2));

        _machine.StopAll(_later).ShouldHaveSingleItem().ShouldBeOfType<PresenceSessionClosed>().Segments.ShouldHaveSingleItem();
    }


    private static ObservedActivity[] Seen(params PresenceActivity[] activities) =>
        [.. activities.Select(activity => new ObservedActivity(activity, PresenceActivityDetails.None))];

    private static PresenceActivityDetails Song(string title, string artists, string album) =>
        new(Details: title, State: artists, LargeText: album);

    /// <summary>
    /// A closed session of an activity without rich fields: one segment covering it.
    /// </summary>
    private static PresenceSessionClosed Closed(Guid userId, PresenceActivity activity, DateTimeOffset startedAt, DateTimeOffset at) =>
        new(userId, activity, startedAt, at, [new PresenceSegment(startedAt, at, PresenceActivityDetails.None)]);
}
