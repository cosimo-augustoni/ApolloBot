using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Auth;

namespace Apollo.Web.Tests.Admin;

/// <summary>The raw event browser of the admin data page (#32): one page of one table, newest first, filtered.</summary>
public sealed class RawEventQueryTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly DateTimeOffset _from = TestTime.Now.AddDays(-1);
    private static readonly DateTimeOffset _to = TestTime.Now;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private EventSeeder Events => new(Fixture, Ct);

    [Fact]
    public async Task RawEvents_ShouldPageNewestFirstWithTotal()
    {
        var oldest = await Events.AddAsync(DataEventType.Message, _to.AddHours(-5));
        var sameTimeFirst = await Events.AddAsync(DataEventType.Message, _to.AddHours(-3));
        var sameTimeSecond = await Events.AddAsync(DataEventType.Message, _to.AddHours(-3));
        var newer = await Events.AddAsync(DataEventType.Message, _to.AddHours(-2));
        var newest = await Events.AddAsync(DataEventType.Message, _to.AddHours(-1));
        await Events.AddAsync(DataEventType.Reaction, _to.AddHours(-1));

        var first = await QueryAsync(new RawEventSearch(DataEventType.Message, null, null, _from, _to, Page: 1, PageSize: 2));
        var second = await QueryAsync(new RawEventSearch(DataEventType.Message, null, null, _from, _to, Page: 2, PageSize: 2));
        var third = await QueryAsync(new RawEventSearch(DataEventType.Message, null, null, _from, _to, Page: 3, PageSize: 2));

        first.Items.Select(e => e.Id).ShouldBe([newest, newer]);
        second.Items.Select(e => e.Id).ShouldBe([sameTimeSecond, sameTimeFirst]);
        third.Items.Select(e => e.Id).ShouldBe([oldest]);
        second.Total.ShouldBe(5);
        third.Total.ShouldBe(5);
    }

    [Fact]
    public async Task RawEvents_ShouldApplyUserChannelAndDateFilters()
    {
        var bob = await Events.UserAsync(TestPlatform.User("bob"), "Bob");
        var memes = await Events.ChannelAsync(TestPlatform.Channel("memes"), "memes");
        var match = await Events.AddAsync(DataEventType.Message, _to.AddHours(-1), bob, memes);
        await Events.AddAsync(DataEventType.Message, _to.AddHours(-1), userId: bob);
        await Events.AddAsync(DataEventType.Message, _to.AddHours(-1), channelId: memes);
        await Events.AddAsync(DataEventType.Message, _from.AddSeconds(-1), bob, memes);
        await Events.AddAsync(DataEventType.Message, _to, bob, memes);
        await Events.AddAsync(DataEventType.Reaction, _to.AddHours(-1), bob, memes);

        var bobInMemes = await QueryAsync(new RawEventSearch(DataEventType.Message, bob, memes, _from, _to, 1, 50));
        var bobAnywhere = await QueryAsync(new RawEventSearch(DataEventType.Message, bob, null, _from, _to, 1, 50));
        var anyoneInMemes = await QueryAsync(new RawEventSearch(DataEventType.Message, null, memes, _from, _to, 1, 50));
        var allTime = await QueryAsync(new RawEventSearch(DataEventType.Message, bob, memes, _from.AddDays(-1), _to.AddDays(1), 1, 50));

        bobInMemes.Items.Select(e => e.Id).ShouldBe([match]);
        bobInMemes.Total.ShouldBe(1);
        bobAnywhere.Total.ShouldBe(2);
        anyoneInMemes.Total.ShouldBe(2);
        allTime.Total.ShouldBe(3);
    }

    [Theory]
    [InlineData(DataEventType.Membership)]
    [InlineData(DataEventType.VoiceStateSegment)]
    [InlineData(DataEventType.PresenceActivity)]
    public async Task RawEvents_WhenChannelFilterOnTableWithoutChannel_ShouldReturnNothing(DataEventType type)
    {
        var general = await Events.ChannelAsync(EventSeeder.General, "general");
        await Events.AddAsync(type, _to.AddHours(-1));

        var page = await QueryAsync(new RawEventSearch(type, null, general, _from, _to, 1, 50));

        page.Total.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task RawEvents_WhenEventsOfAnotherGuild_ShouldNotReturnThem()
    {
        await Events.AddAsync(DataEventType.Message, _to.AddHours(-1), guild: new PlatformRef(PlatformKind.Discord, "333333333333333333"));

        var page = await QueryAsync(new RawEventSearch(DataEventType.Message, null, null, _from, _to, 1, 50));

        page.Total.ShouldBe(0);
    }

    [Fact]
    public async Task RawEvents_WhenReferencedUserNull_ShouldReturnNullReference()
    {
        var alice = await Events.UserAsync(EventSeeder.Alice, "Alice");
        var bob = await Events.UserAsync(TestPlatform.User("bob"), "Bob");
        var untracked = await Events.AddAsync(DataEventType.Reaction, _to.AddHours(-2), alice);
        var tracked = await Events.AddAsync(DataEventType.Reaction, _to.AddHours(-1), alice, referencedUserId: bob);

        var page = await QueryAsync(new RawEventSearch(DataEventType.Reaction, null, null, _from, _to, 1, 50));

        page.Items.Single(e => e.Id == untracked).Reaction.ShouldNotBeNull().MessageAuthor.ShouldBeNull();
        page.Items.Single(e => e.Id == tracked).Reaction.ShouldNotBeNull().MessageAuthor.ShouldBe(new EventUserDto(bob, "Bob"));
    }

    [Fact]
    public async Task RawEvents_WhenMessage_ShouldReturnUserChannelAndMetadata()
    {
        var alice = await Events.UserAsync(EventSeeder.Alice, "Alice");
        var bob = await Events.UserAsync(TestPlatform.User("bob"), "Bob");
        var general = await Events.ChannelAsync(EventSeeder.General, "allgemein");
        var id = await Events.AddAsync(DataEventType.Message, _to.AddHours(-1), alice, general, referencedUserId: bob);
        var page = await QueryAsync(new RawEventSearch(DataEventType.Message, null, null, _from, _to, 1, 50));

        var message = page.Items.ShouldHaveSingleItem();
        message.ShouldBe(new RawEventDto(id, DataEventType.Message, _to.AddHours(-1), new EventUserDto(alice, "Alice"), new EventChannelDto(general, "allgemein"))
        {
            Message = message.Message,
        });
        var details = message.Message.ShouldNotBeNull();
        (details with { Parts = null }).ShouldBe(new MessageEventDetailsDto(
            "Create", 42, 7, 1, 0, 0, 0, 0, false, null, null, new EventUserDto(bob, "Bob"), IsBot: false));
        details.Parts.ShouldBe(["Text", "Attachment"]);
    }

    [Theory]
    [InlineData(DataEventType.VoiceSession)]
    [InlineData(DataEventType.VoiceStateSegment)]
    [InlineData(DataEventType.Reaction)]
    [InlineData(DataEventType.Membership)]
    [InlineData(DataEventType.PresenceActivity)]
    public async Task RawEvents_ShouldFillTheDetailsOfTheirTypeOnly(DataEventType type)
    {
        var alice = await Events.UserAsync(EventSeeder.Alice, "Alice");
        await Events.AddAsync(type, _to.AddHours(-1), alice);

        var page = await QueryAsync(new RawEventSearch(type, null, null, _from, _to, 1, 50));

        var row = page.Items.ShouldHaveSingleItem();
        row.Type.ShouldBe(type);
        row.User.ShouldBe(new EventUserDto(alice, "Alice"));
        object?[] details = [row.Message, row.VoiceSession, row.VoiceSegment, row.Reaction, row.Membership, row.PresenceActivity];
        details.Count(d => d is not null).ShouldBe(1);
        details[(int)type].ShouldNotBeNull();
        (row.Channel is not null).ShouldBe(type is DataEventType.VoiceSession or DataEventType.Reaction);
    }

    [Fact]
    public async Task RawEvents_WhenDetailsOfEachType_ShouldMapTheStoredValues()
    {
        await Events.AddAsync(DataEventType.VoiceSession, _to.AddHours(-1));
        await Events.AddAsync(DataEventType.VoiceStateSegment, _to.AddHours(-1));
        await Events.AddAsync(DataEventType.Reaction, _to.AddHours(-1));
        await Events.AddAsync(DataEventType.Membership, _to.AddHours(-1));
        await Events.AddAsync(DataEventType.PresenceActivity, _to.AddHours(-1));

        (await SingleAsync(DataEventType.VoiceSession)).VoiceSession.ShouldBe(new VoiceSessionDetailsDto(null, null));
        (await SingleAsync(DataEventType.VoiceStateSegment)).VoiceSegment.ShouldBe(new VoiceSegmentDetailsDto(1, null, SelfMute: true, SelfDeaf: false, Streaming: false, Video: false));
        (await SingleAsync(DataEventType.Reaction)).Reaction.ShouldBe(new ReactionEventDetailsDto("Add", "👍", IsCustomEmoji: false, MessageAuthor: null));
        (await SingleAsync(DataEventType.Membership)).Membership.ShouldBe(new MembershipEventDetailsDto("Join"));
        (await SingleAsync(DataEventType.PresenceActivity)).PresenceActivity.ShouldBe(new PresenceActivityDetailsDto("Playing", "Factorio", _to.AddMinutes(-55)));
    }

    private async Task<RawEventDto> SingleAsync(DataEventType type) =>
        (await QueryAsync(new RawEventSearch(type, null, null, _from, _to, 1, 50))).Items.ShouldHaveSingleItem();

    private async Task<RawEventPageDto> QueryAsync(RawEventSearch search)
    {
        await using var db = Fixture.CreateDbContext();
        return await new RawEventQuery(db, [new FakeIdentityProvider()]).GetPageAsync(search, Ct);
    }
}
