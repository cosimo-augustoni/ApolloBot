using System.Diagnostics.Metrics;
using Apollo.Bot.Messages;
using Apollo.Bot.Reactions;
using Apollo.Bot.Tests.Tracking;
using Apollo.Data.Reactions;
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

namespace Apollo.Bot.Tests.Reactions;

public sealed class ReactionRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");

    // alice and bob hold the tracking role; carol does not; dave is no member at all.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");
    private static readonly PlatformRef _dave = TestPlatform.User("dave");

    private static readonly PlatformRef _general = TestPlatform.Channel("general");
    private static readonly PlatformRef _thread = TestPlatform.Channel("thread");

    private static readonly PlatformRef _message = TestPlatform.Message("m1");

    private static readonly PlatformEmoji _thumbsUp = new("👍", CustomEmoji: null);
    private static readonly PlatformEmoji _partyBlob = new("partyblob", TestPlatform.Emoji("partyblob"));

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task Add_WhenUserIsTrackedAndTheEmojiIsUnicode_ShouldStoreOneAddRowWithoutCustomId()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_alice).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        var (guildId, aliceId, generalId) = (await GuildIdAsync(host), await host.UserIdAsync(_alice), await ChannelIdAsync(host, _general));
        row.ShouldSatisfyAllConditions(
            () => row.GuildId.ShouldBe(guildId),
            () => row.OccurredAt.ShouldBe(TestTime.Now),
            () => row.UserId.ShouldBe(aliceId),
            () => row.ChannelId.ShouldBe(generalId),
            () => row.MessageExtId.ShouldBe(_message.ExternalId),
            () => row.MessageAuthorId.ShouldBeNull(),
            () => row.EmojiName.ShouldBe("👍"),
            () => row.EmojiCustomId.ShouldBeNull(),
            () => row.Kind.ShouldBe(ReactionEventKind.Add));
    }

    [Fact]
    public async Task Add_WhenTheEmojiIsCustom_ShouldStoreItsIdAndName()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_alice).WithEmoji(_partyBlob).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        row.EmojiName.ShouldBe("partyblob");
        row.EmojiCustomId.ShouldBe(_partyBlob.CustomEmoji!.ExternalId);
    }

    [Fact]
    public async Task Remove_WhenUserIsTracked_ShouldStoreOneRemoveRow()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Removed(_alice).WithEmoji(_partyBlob).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(
            () => row.Kind.ShouldBe(ReactionEventKind.Remove),
            () => row.MessageExtId.ShouldBe(_message.ExternalId),
            () => row.EmojiName.ShouldBe("partyblob"),
            () => row.EmojiCustomId.ShouldBe(_partyBlob.CustomEmoji!.ExternalId));
    }

    [Fact]
    public async Task Add_WhenUserIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_carol).OnMessage(_message, _alice).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_WhenUserIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Removed(_carol).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_WhenTheMessageAuthorIsTracked_ShouldStoreTheirId()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_alice).OnMessage(_message, _bob).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().MessageAuthorId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Add_WhenTheMessageAuthorIsUntracked_ShouldStoreNullAuthor()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_alice).OnMessage(_message, _carol).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().MessageAuthorId.ShouldBeNull();
    }

    [Fact]
    public async Task Add_WhenTheMessageAuthorWasNeverSeen_ShouldCreateNoRowForThem()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_alice).OnMessage(_message, _dave).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().MessageAuthorId.ShouldBeNull();
        (await host.InScopeAsync((_, ids) => ids.TryGetUserAsync(_dave, TestContext.Current.CancellationToken))).ShouldBeNull();
    }

    [Fact]
    public async Task Remove_WithoutAuthor_ShouldTakeItFromTheMatchingAddRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Added(_alice).OnMessage(_message, _bob).WithEmoji(_partyBlob).Build());

        await host.RaiseAsync(Removed(_alice).WithEmoji(_partyBlob).Build());

        var remove = (await EventsAsync(host)).Single(e => e.Kind == ReactionEventKind.Remove);
        remove.MessageAuthorId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Remove_WithoutAuthor_ShouldTakeItFromTheMatchingUnicodeAddRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Added(_alice).OnMessage(_message, _bob).WithEmoji(_thumbsUp).Build());

        await host.RaiseAsync(Removed(_alice).WithEmoji(_thumbsUp).Build());

        var remove = (await EventsAsync(host)).Single(e => e.Kind == ReactionEventKind.Remove);
        remove.MessageAuthorId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Remove_WithoutAuthor_WhenSeveralAddRowsMatch_ShouldTakeTheLatestOnesAuthor()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Added(_alice).OnMessage(_message, _bob).At(TestTime.Now.AddMinutes(-1)).Build());
        await host.RaiseAsync(Added(_alice).OnMessage(_message, _alice).At(TestTime.Now.AddMinutes(-2)).Build());

        await host.RaiseAsync(Removed(_alice).Build());

        var remove = (await EventsAsync(host)).Single(e => e.Kind == ReactionEventKind.Remove);
        remove.MessageAuthorId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Remove_WithoutAuthor_WhenTheOnlyAddRowIsForAnotherEmoji_ShouldNotTakeItsAuthor()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Added(_alice).OnMessage(_message, _bob).WithEmoji(_partyBlob).Build());

        await host.RaiseAsync(Removed(_alice).WithEmoji(_thumbsUp).Build());

        (await EventsAsync(host)).Single(e => e.Kind == ReactionEventKind.Remove).MessageAuthorId.ShouldBeNull();
    }

    [Fact]
    public async Task Remove_WithoutAuthor_WhenTheOnlyAddRowIsAnotherUsers_ShouldNotTakeItsAuthor()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Added(_bob).OnMessage(_message, _bob).Build());

        await host.RaiseAsync(Removed(_alice).Build());

        (await EventsAsync(host)).Single(e => e.Kind == ReactionEventKind.Remove).MessageAuthorId.ShouldBeNull();
    }

    [Fact]
    public async Task Remove_WithoutAuthorAndWithoutAddRow_ShouldTakeItFromTheMessageCreateRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(new PlatformMessageCreatedBuilder().WithMessage(_message).ByAuthor(_bob).Build());

        await host.RaiseAsync(Removed(_alice).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().MessageAuthorId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Add_WithoutAuthor_ShouldTakeItFromTheMessageCreateRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(new PlatformMessageCreatedBuilder().WithMessage(_message).ByAuthor(_bob).Build());

        await host.RaiseAsync(Added(_alice).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().MessageAuthorId.ShouldBe(await host.UserIdAsync(_bob));
    }

    [Fact]
    public async Task Remove_WithoutAuthorAndWithoutAnyEarlierRow_ShouldStoreNullAuthor()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Removed(_alice).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().MessageAuthorId.ShouldBeNull();
    }

    [Fact]
    public async Task Remove_WithoutAuthor_WhenTheAuthorIsNoLongerTracked_ShouldStoreNullAuthor()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Added(_alice).OnMessage(_message, _bob).Build());
        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_bob)).Build());

        await host.RaiseAsync(Removed(_alice).Build());

        (await EventsAsync(host)).Single(e => e.Kind == ReactionEventKind.Remove).MessageAuthorId.ShouldBeNull();
    }

    [Fact]
    public async Task Add_WhenTheMessageIsInAThread_ShouldStoreTheThreadAsChannel()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Added(_alice).InChannel(_thread).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().ChannelId.ShouldBe(await ChannelIdAsync(host, _thread));
    }

    [Fact]
    public async Task Add_WhenStored_ShouldCountItAsRecordedWithItsEventType()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);

        await host.RaiseAsync(Added(_alice).Build());
        await host.RaiseAsync(Removed(_alice).Build());

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe([nameof(PlatformReactionAdded), nameof(PlatformReactionRemoved)]);
    }

    [Fact]
    public async Task Add_WhenTheGateDropsIt_ShouldCountItAsDroppedUntracked()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await host.RaiseAsync(Added(_carol).Build());
        await host.RaiseAsync(Removed(_carol).Build());

        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked", "untracked"]);
    }

    public void Dispose() => _meters.Dispose();

    private static PlatformReactionAddedBuilder Added(PlatformRef user) =>
        new PlatformReactionAddedBuilder().OnMessage(_message).ByUser(user).WithEmoji(_thumbsUp);

    private static PlatformReactionRemovedBuilder Removed(PlatformRef user) =>
        new PlatformReactionRemovedBuilder().OnMessage(_message).ByUser(user).WithEmoji(_thumbsUp);

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();

    private async Task<TrackingHost> HostAsync()
    {
        var host = new TrackingHost(Fixture, services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            services.AddMessageRecording();
            services.AddReactionRecording();
        });
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build())
            .WithChannels(
                new PlatformChannelBuilder().WithRef(_general).Named("general").Build(),
                new PlatformChannelBuilder().WithRef(_thread).Named("thread").AsThreadOf(_general).Build())
            .WithMembers(Member(_alice, _statistik), Member(_bob, _statistik), Member(_carol))
            .Build());
        return host;
    }

    private static Task<List<ReactionEvent>> EventsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.ReactionEvents.AsNoTracking().OrderBy(e => e.Id).ToListAsync(TestContext.Current.CancellationToken));

    private static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());

    private static Task<Guid> ChannelIdAsync(TrackingHost host, PlatformRef channel) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetChannelAsync(channel, TestContext.Current.CancellationToken)).GetValueOrDefault());
}
