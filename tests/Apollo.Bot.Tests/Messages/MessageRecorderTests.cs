using Apollo.Bot.Messages;
using Apollo.Bot.Tests.Tracking;
using Apollo.Data.Messages;
using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;

namespace Apollo.Bot.Tests.Messages;

public sealed class MessageRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");

    // alice, bob and the bot hold the tracking role; carol does not.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");
    private static readonly PlatformRef _botty = TestPlatform.User("botty");

    private static readonly PlatformRef _general = TestPlatform.Channel("general");
    private static readonly PlatformRef _thread = TestPlatform.Channel("thread");

    private static readonly PlatformRef _message = TestPlatform.Message("m1");
    private static readonly PlatformRef _otherMessage = TestPlatform.Message("m0");

    private static readonly MessageMetadata _metadata = new()
    {
        Length = 42,
        WordCount = 7,
        AttachmentCount = 1,
        EmbedCount = 2,
        EmojiCount = 3,
        UserMentionCount = 2,
        RoleMentionCount = 1,
        MentionsEveryone = true,
        ContentParts = MessageContentParts.Text | MessageContentParts.Mention | MessageContentParts.Attachment,
    };

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task Create_WhenAuthorIsTracked_ShouldStoreOneCreateRowWithItsMetadata()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).WithMetadata(_metadata).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        var (guildId, aliceId, generalId) = (await GuildIdAsync(host), await host.UserIdAsync(_alice), await ChannelIdAsync(host, _general));
        row.ShouldSatisfyAllConditions(
            () => row.GuildId.ShouldBe(guildId),
            () => row.OccurredAt.ShouldBe(TestTime.Now),
            () => row.UserId.ShouldBe(aliceId),
            () => row.ChannelId.ShouldBe(generalId),
            () => row.ThreadParentId.ShouldBeNull(),
            () => row.MessageExtId.ShouldBe(_message.ExternalId),
            () => row.Kind.ShouldBe(MessageEventKind.Create),
            () => row.Length.ShouldBe(42),
            () => row.WordCount.ShouldBe(7),
            () => row.AttachmentCount.ShouldBe(1),
            () => row.EmbedCount.ShouldBe(2),
            () => row.EmojiCount.ShouldBe(3),
            () => row.UserMentionCount.ShouldBe(2),
            () => row.RoleMentionCount.ShouldBe(1),
            () => row.MentionsEveryone.ShouldBe(true),
            () => row.ContentParts.ShouldBe(MessageContentParts.Text | MessageContentParts.Mention | MessageContentParts.Attachment),
            () => row.ReplyToUserId.ShouldBeNull(),
            () => row.IsBot.ShouldBeFalse());
    }

    [Fact]
    public async Task Create_WhenAuthorIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_carol).Mentioning(_alice).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
        (await MentionsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WhenReplyingToAnUntrackedUser_ShouldStoreNullReplyTo()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).ReplyingTo(_otherMessage, _carol).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().ReplyToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task Create_WhenReplyingToATrackedUser_ShouldStoreTheirId()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).ReplyingTo(_otherMessage, _bob).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().ReplyToUserId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Create_WhenReplyHasNoAuthor_ShouldTakeItFromTheCreateRowOfTheRepliedToMessage()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_bob, _otherMessage).Build());

        await host.RaiseAsync(Created(_alice).ReplyingTo(_otherMessage).Build());

        var reply = (await EventsAsync(host)).Single(e => e.MessageExtId == _message.ExternalId);
        reply.ReplyToUserId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Create_WhenReplyHasNoAuthorAndTheRepliedToMessageIsUnknown_ShouldStoreNullReplyTo()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).ReplyingTo(_otherMessage).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().ReplyToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task Create_WhenReplyHasNoAuthorAndTheRepliedToAuthorIsNoLongerTracked_ShouldStoreNullReplyTo()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_bob, _otherMessage).Build());
        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_bob)).Build());

        await host.RaiseAsync(Created(_alice).ReplyingTo(_otherMessage).Build());

        (await EventsAsync(host)).Single(e => e.MessageExtId == _message.ExternalId).ReplyToUserId.ShouldBeNull();
    }

    [Fact]
    public async Task Create_WhenMessageIsInAThread_ShouldStoreTheThreadAndItsParent()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).InChannel(_thread).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        row.ChannelId.ShouldBe(await ChannelIdAsync(host, _thread));
        row.ThreadParentId.ShouldBe(await ChannelIdAsync(host, _general));
    }

    [Fact]
    public async Task Create_WhenAuthorIsABot_ShouldStoreIsBot()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(new PlatformMessageCreatedBuilder().WithMessage(_message).ByAuthor(_botty, isBot: true).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().IsBot.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WhenMentioningATrackedAndAnUntrackedUserTwiceEach_ShouldStoreOneMentionRowPerUser()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).Mentioning(_bob, _carol, _bob, _carol).Build());

        var mentions = await MentionsAsync(host);
        mentions.Select(m => m.MentionedUserId).ShouldBe([await host.UserIdAsync(_bob), null], ignoreOrder: true);
        var (aliceId, generalId) = (await host.UserIdAsync(_alice), await ChannelIdAsync(host, _general));
        mentions.ShouldAllBe(m => m.UserId == aliceId && m.ChannelId == generalId && m.MessageExtId == "m1" && m.OccurredAt == TestTime.Now);
    }

    [Fact]
    public async Task Create_WhenMentioningThemselves_ShouldStoreTheSelfMention()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Created(_alice).Mentioning(_alice).Build());

        (await MentionsAsync(host)).ShouldHaveSingleItem().MentionedUserId.ShouldBe(await host.UserIdAsync(_alice));
    }

    [Fact]
    public async Task Edit_ShouldStoreAnEditRowWithTheUpdatedMetadata()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_alice).Build());

        await host.RaiseAsync(Updated(_alice).WithMetadata(_metadata).At(TestTime.Now.AddMinutes(1)).Build());

        var edit = (await EventsAsync(host)).Single(e => e.Kind == MessageEventKind.Edit);
        edit.OccurredAt.ShouldBe(TestTime.Now.AddMinutes(1));
        edit.UserId.ShouldBe(await host.UserIdAsync(_alice));
        edit.Length.ShouldBe(42);
        edit.ContentParts.ShouldBe(_metadata.ContentParts);
    }

    [Fact]
    public async Task Edit_WhenItAddsAMention_ShouldUpdateTheCountsButWriteNoMentionRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_alice).WithMetadata(MessageMetadata.From("hi", 0, 0)).Build());

        await host.RaiseAsync(Updated(_alice).WithMetadata(MessageMetadata.From("hi <@1>", 0, 0)).Build());

        var edit = (await EventsAsync(host)).Single(e => e.Kind == MessageEventKind.Edit);
        edit.UserMentionCount.ShouldBe(1);
        edit.ContentParts.ShouldBe(MessageContentParts.Text | MessageContentParts.Mention);
        (await MentionsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_WhenAuthorIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Updated(_carol).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_WhenAuthorIsMissing_ShouldTakeItFromTheCreateRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_botty).ByAuthor(_botty, isBot: true).Build());

        await host.RaiseAsync(Updated(author: null).Build());

        var edit = (await EventsAsync(host)).Single(e => e.Kind == MessageEventKind.Edit);
        edit.UserId.ShouldBe(await host.UserIdAsync(_botty));
        edit.IsBot.ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_WithAuthor_ShouldStoreADeleteRow()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Deleted(_alice).Build());

        var delete = (await EventsAsync(host)).ShouldHaveSingleItem();
        delete.Kind.ShouldBe(MessageEventKind.Delete);
        delete.UserId.ShouldBe(await host.UserIdAsync(_alice));
        delete.MessageExtId.ShouldBe("m1");
    }

    [Fact]
    public async Task Delete_ShouldLeaveTheContentMetadataColumnsNull()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_alice).WithMetadata(_metadata).Build());

        await host.RaiseAsync(Deleted(author: null).Build());

        var delete = (await EventsAsync(host)).Single(e => e.Kind == MessageEventKind.Delete);
        delete.ShouldSatisfyAllConditions(
            () => delete.Length.ShouldBeNull(),
            () => delete.WordCount.ShouldBeNull(),
            () => delete.AttachmentCount.ShouldBeNull(),
            () => delete.EmbedCount.ShouldBeNull(),
            () => delete.EmojiCount.ShouldBeNull(),
            () => delete.UserMentionCount.ShouldBeNull(),
            () => delete.RoleMentionCount.ShouldBeNull(),
            () => delete.MentionsEveryone.ShouldBeNull(),
            () => delete.ContentParts.ShouldBeNull(),
            () => delete.ReplyToUserId.ShouldBeNull());
    }

    [Fact]
    public async Task Delete_WithoutAuthor_ShouldTakeTheUserFromTheCreateRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_alice).Build());

        await host.RaiseAsync(Deleted(author: null).Build());

        var delete = (await EventsAsync(host)).Single(e => e.Kind == MessageEventKind.Delete);
        delete.UserId.ShouldBe(await host.UserIdAsync(_alice));
        delete.ChannelId.ShouldBe(await ChannelIdAsync(host, _general));
    }

    [Fact]
    public async Task Delete_WithoutAuthorAndWithoutACreateRow_ShouldStoreNothing()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Created(_carol).Build());

        await host.RaiseAsync(Deleted(author: null).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Create_WhenStored_ShouldCountItAsRecordedWithItsEventType()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);

        await host.RaiseAsync(Created(_alice).Build());
        await host.RaiseAsync(Deleted(author: null).Build());

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe([nameof(PlatformMessageCreated), nameof(PlatformMessageDeleted)]);
    }

    [Fact]
    public async Task Create_WhenTheGateDropsIt_ShouldCountItAsDroppedUntracked()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await host.RaiseAsync(Created(_carol).Build());
        await host.RaiseAsync(Updated(_carol).Build());
        await host.RaiseAsync(Deleted(_carol).Build());

        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked", "untracked", "untracked"]);
    }

    public void Dispose() => _meters.Dispose();

    private static PlatformMessageCreatedBuilder Created(PlatformRef author, PlatformRef? message = null) =>
        new PlatformMessageCreatedBuilder().WithMessage(message ?? _message).ByAuthor(author);

    private static PlatformMessageUpdatedBuilder Updated(PlatformRef? author) =>
        new PlatformMessageUpdatedBuilder().WithMessage(_message).ByAuthor(author);

    private static PlatformMessageDeletedBuilder Deleted(PlatformRef? author) =>
        new PlatformMessageDeletedBuilder().WithMessage(_message).ByAuthor(author);

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();

    private async Task<TrackingHost> HostAsync()
    {
        var host = new TrackingHost(Fixture, services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            services.AddMessageRecording();
        });
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build())
            .WithChannels(
                new PlatformChannelBuilder().WithRef(_general).Named("general").Build(),
                new PlatformChannelBuilder().WithRef(_thread).Named("thread").AsThreadOf(_general).Build())
            .WithMembers(
                Member(_alice, _statistik),
                Member(_bob, _statistik),
                Member(_carol),
                new PlatformMemberBuilder().ForUser(_botty).WithRoles(_statistik).AsBot().Build())
            .Build());
        return host;
    }

    private static Task<List<MessageEvent>> EventsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.MessageEvents.AsNoTracking().OrderBy(e => e.Id).ToListAsync(TestContext.Current.CancellationToken));

    private static Task<List<MessageMention>> MentionsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.MessageMentions.AsNoTracking().OrderBy(e => e.Id).ToListAsync(TestContext.Current.CancellationToken));

    private static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());

    private static Task<Guid> ChannelIdAsync(TrackingHost host, PlatformRef channel) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetChannelAsync(channel, TestContext.Current.CancellationToken)).GetValueOrDefault());
}
