using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordMessageHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;
    private const ulong ChannelId = 500;
    private const ulong AliceId = 42;
    private const ulong BobId = 43;

    private static readonly ulong _messageId = NetCordPayloads.Snowflake(TestTime.Now.AddMinutes(-1));

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task MessageCreate_ShouldDispatchMessageCreatedWithItsReferencesAndMetadata()
    {
        var message = NetCordPayloads.GuildMessage(
            DiscordHost.GuildId,
            _messageId,
            AliceId,
            content: "hi <@43> <@&7> look https://example.com 😀",
            channelId: ChannelId,
            mentions: [BobId],
            mentionRoles: [7],
            attachments: 2,
            embeds: 1);

        await Create(message);

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>();
        created.ShouldSatisfyAllConditions(
            () => created.Guild.ShouldBe(Ref(DiscordHost.GuildId)),
            () => created.OccurredAt.ShouldBe(TestTime.Now.AddMinutes(-1)),
            () => created.Channel.ShouldBe(Ref(ChannelId)),
            () => created.Message.ShouldBe(Ref(_messageId)),
            () => created.Author.ShouldBe(Ref(AliceId)),
            () => created.AuthorIsBot.ShouldBeFalse(),
            () => created.ReplyToMessage.ShouldBeNull(),
            () => created.ReplyToAuthor.ShouldBeNull(),
            () => created.MentionedUsers.ShouldBe([Ref(BobId)]));
        created.Metadata.ShouldBe(new MessageMetadata
        {
            Length = 41,
            WordCount = 2,
            AttachmentCount = 2,
            EmbedCount = 1,
            EmojiCount = 1,
            UserMentionCount = 1,
            RoleMentionCount = 1,
            MentionsEveryone = false,
            ContentParts = MessageContentParts.Text | MessageContentParts.Link | MessageContentParts.Mention
                | MessageContentParts.Emoji | MessageContentParts.Attachment,
        });
    }

    [Fact]
    public async Task MessageCreate_WhenAuthorIsABot_ShouldSetAuthorIsBot()
    {
        await Create(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, bot: true));

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>().AuthorIsBot.ShouldBeTrue();
    }

    [Fact]
    public async Task MessageCreate_WhenEveryoneDidNotPing_ShouldTreatItAsText()
    {
        await Create(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, content: "@everyone", mentionEveryone: false));

        var metadata = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>().Metadata;
        metadata.MentionsEveryone.ShouldBeFalse();
        metadata.ContentParts.ShouldBe(MessageContentParts.Text);
    }

    [Fact]
    public async Task MessageCreate_WhenReplyPingsTheRepliedToAuthor_ShouldReferenceTheReplyButNotCountThePingAsAMention()
    {
        var message = NetCordPayloads.GuildMessage(
            DiscordHost.GuildId, _messageId, AliceId, content: "agreed", type: 19, mentions: [BobId], replyTo: (77, BobId));

        await Create(message);

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>();
        created.ReplyToMessage.ShouldBe(Ref(77));
        created.ReplyToAuthor.ShouldBe(Ref(BobId));
        created.MentionedUsers.ShouldBeEmpty();
        created.Metadata.UserMentionCount.ShouldBe(0);
        created.Metadata.ContentParts.ShouldBe(MessageContentParts.Text);
    }

    [Fact]
    public async Task MessageCreate_WhenReplyAlsoMentionsTheRepliedToAuthorInTheText_ShouldKeepTheMention()
    {
        var message = NetCordPayloads.GuildMessage(
            DiscordHost.GuildId, _messageId, AliceId, content: "<@!43> agreed", type: 19, mentions: [BobId], replyTo: (77, BobId));

        await Create(message);

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>().MentionedUsers.ShouldBe([Ref(BobId)]);
    }

    [Fact]
    public async Task MessageCreate_WhenRepliedToMessageIsMissingFromThePayload_ShouldLeaveTheAuthorToTheRecorder()
    {
        await Create(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, type: 19, replyTo: (77, null)));

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>();
        created.ReplyToMessage.ShouldBe(Ref(77));
        created.ReplyToAuthor.ShouldBeNull();
    }

    [Fact]
    public async Task MessageCreate_WhenMessageIsAForward_ShouldNotBeAReply()
    {
        await Create(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, content: "", forwardOf: 77));

        _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageCreated>().ReplyToMessage.ShouldBeNull();
    }

    [Theory]
    [InlineData(7)] // member join
    [InlineData(6)] // pin notice
    [InlineData(18)] // thread created
    public async Task MessageCreate_WhenMessageIsASystemMessage_ShouldNotDispatch(int type)
    {
        await Create(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, content: "", type: type));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task MessageCreate_WhenMessageIsFromAnotherGuild_ShouldNotDispatch()
    {
        await Create(NetCordPayloads.GuildMessage(ForeignGuildId, _messageId, AliceId));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task MessageCreate_ShouldNotPassTheContentOn()
    {
        await Create(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, content: "zebra quokka narwhal"));

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem();
        created.ToString().ShouldNotContain("zebra");
        created.ToString().ShouldNotContain("quokka");
    }

    [Fact]
    public async Task MessageUpdate_WhenMessageWasEdited_ShouldDispatchMessageUpdatedAtTheEditTime()
    {
        var editedAt = TestTime.Now.AddSeconds(-5);
        var message = NetCordPayloads.GuildMessage(
            DiscordHost.GuildId, _messageId, AliceId, content: "now <@43>", channelId: ChannelId, mentions: [BobId], editedAt: editedAt);

        await Update(message);

        var updated = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageUpdated>();
        updated.ShouldSatisfyAllConditions(
            () => updated.OccurredAt.ShouldBe(editedAt),
            () => updated.Channel.ShouldBe(Ref(ChannelId)),
            () => updated.Message.ShouldBe(Ref(_messageId)),
            () => updated.Author.ShouldBe(Ref(AliceId)),
            () => updated.Metadata.UserMentionCount.ShouldBe(1),
            () => updated.Metadata.ContentParts.ShouldBe(MessageContentParts.Text | MessageContentParts.Mention));
    }

    [Fact]
    public async Task MessageUpdate_WhenOnlyEmbedsWereResolved_ShouldNotDispatch()
    {
        await Update(NetCordPayloads.GuildMessage(DiscordHost.GuildId, _messageId, AliceId, content: "https://example.com", embeds: 1));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task MessageDelete_ShouldDispatchMessageDeletedWithoutAuthor()
    {
        await Handler().HandleAsync(NetCordPayloads.MessageDelete(DiscordHost.GuildId, _messageId, ChannelId));

        var deleted = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMessageDeleted>();
        deleted.OccurredAt.ShouldBe(TestTime.Now);
        deleted.Channel.ShouldBe(Ref(ChannelId));
        deleted.Message.ShouldBe(Ref(_messageId));
        deleted.Author.ShouldBeNull();
    }

    [Fact]
    public async Task MessageDelete_WhenMessageIsFromAnotherGuild_ShouldNotDispatch()
    {
        await Handler().HandleAsync(NetCordPayloads.MessageDelete(ForeignGuildId, _messageId));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task MessageDeleteBulk_ShouldDispatchOneMessageDeletedPerMessage()
    {
        await Handler().HandleAsync(NetCordPayloads.MessageDeleteBulk(DiscordHost.GuildId, ChannelId, 1, 2, 3));

        _harness.Dispatcher.Events.Cast<PlatformMessageDeleted>().Select(d => d.Message).ShouldBe([Ref(1), Ref(2), Ref(3)]);
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private ValueTask Create(NetCord.Gateway.Message message) => ((IMessageCreateGatewayHandler)Handler()).HandleAsync(message);

    private ValueTask Update(NetCord.Gateway.Message message) => ((IMessageUpdateGatewayHandler)Handler()).HandleAsync(message);

    private DiscordMessageHandler Handler() => new(_harness.Mapper, _harness.Time);
}
