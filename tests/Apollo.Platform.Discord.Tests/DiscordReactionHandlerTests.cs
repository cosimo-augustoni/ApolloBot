using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordReactionHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;
    private const ulong ChannelId = 500;
    private const ulong MessageId = 900;
    private const ulong AliceId = 42;
    private const ulong BobId = 43;
    private const ulong PartyBlobId = 700;

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task ReactionAdd_WithAUnicodeEmoji_ShouldDispatchReactionAddedStampedOnReceipt()
    {
        await AddAsync(NetCordPayloads.ReactionAdd(DiscordHost.GuildId, ChannelId, MessageId, AliceId, "👍", messageAuthorId: BobId));

        var added = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformReactionAdded>();
        added.ShouldBe(new PlatformReactionAdded(
            Guild: Ref(DiscordHost.GuildId),
            OccurredAt: TestTime.Now,
            Channel: Ref(ChannelId),
            Message: Ref(MessageId),
            User: Ref(AliceId),
            MessageAuthor: Ref(BobId),
            Emoji: new PlatformEmoji("👍", CustomEmoji: null)));
    }

    [Fact]
    public async Task ReactionAdd_WithACustomEmoji_ShouldDispatchItsIdAndName()
    {
        await AddAsync(NetCordPayloads.ReactionAdd(DiscordHost.GuildId, ChannelId, MessageId, AliceId, "partyblob", PartyBlobId));

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformReactionAdded>()
            .Emoji.ShouldBe(new PlatformEmoji("partyblob", Ref(PartyBlobId)));
    }

    [Fact]
    public async Task ReactionAdd_WithACustomEmojiWhoseNameIsUnavailable_ShouldDispatchItWithAnEmptyName()
    {
        await AddAsync(NetCordPayloads.ReactionAdd(DiscordHost.GuildId, ChannelId, MessageId, AliceId, emojiName: null, PartyBlobId));

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformReactionAdded>()
            .Emoji.ShouldBe(new PlatformEmoji(string.Empty, Ref(PartyBlobId)));
    }

    [Fact]
    public async Task ReactionAdd_WithoutAMessageAuthor_ShouldLeaveItForTheRecorder()
    {
        await AddAsync(NetCordPayloads.ReactionAdd(DiscordHost.GuildId, ChannelId, MessageId, AliceId, "👍", messageAuthorId: null));

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformReactionAdded>().MessageAuthor.ShouldBeNull();
    }

    [Fact]
    public async Task ReactionAdd_WhenFromAnotherGuild_ShouldDispatchNothing()
    {
        await AddAsync(NetCordPayloads.ReactionAdd(ForeignGuildId, ChannelId, MessageId, AliceId, "👍"));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReactionRemove_ShouldDispatchReactionRemovedWithoutAMessageAuthor()
    {
        await RemoveAsync(NetCordPayloads.ReactionRemove(DiscordHost.GuildId, ChannelId, MessageId, AliceId, "partyblob", PartyBlobId));

        var removed = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformReactionRemoved>();
        removed.ShouldBe(new PlatformReactionRemoved(
            Guild: Ref(DiscordHost.GuildId),
            OccurredAt: TestTime.Now,
            Channel: Ref(ChannelId),
            Message: Ref(MessageId),
            User: Ref(AliceId),
            MessageAuthor: null,
            Emoji: new PlatformEmoji("partyblob", Ref(PartyBlobId))));
    }

    [Fact]
    public async Task ReactionRemove_WhenFromAnotherGuild_ShouldDispatchNothing()
    {
        await RemoveAsync(NetCordPayloads.ReactionRemove(ForeignGuildId, ChannelId, MessageId, AliceId, "👍"));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private DiscordReactionHandler Handler() => new(_harness.Mapper, _harness.Time);

    private ValueTask AddAsync(MessageReactionAddEventArgs args) => ((IMessageReactionAddGatewayHandler)Handler()).HandleAsync(args);

    private ValueTask RemoveAsync(MessageReactionRemoveEventArgs args) => ((IMessageReactionRemoveGatewayHandler)Handler()).HandleAsync(args);
}
