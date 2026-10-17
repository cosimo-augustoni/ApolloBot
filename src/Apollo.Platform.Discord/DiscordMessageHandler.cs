using System.Globalization;
using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps MESSAGE_CREATE / UPDATE / DELETE / DELETE_BULK to the neutral message events (#15). The content is turned into
/// <see cref="MessageMetadata"/> here and goes no further (scope §5.1).
/// </summary>
/// <remarks>
/// <para>Only messages people write are recorded: system messages (joins, pins, boosts, thread notices) are skipped.</para>
/// <para>
/// Discord also sends MESSAGE_UPDATE when it resolves a link's embed; only updates with an <c>edited_timestamp</c> are
/// edits.
/// </para>
/// <para>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</para>
/// </remarks>
internal sealed class DiscordMessageHandler(DiscordEventMapper mapper, TimeProvider timeProvider)
    : IMessageCreateGatewayHandler, IMessageUpdateGatewayHandler, IMessageDeleteGatewayHandler, IMessageDeleteBulkGatewayHandler
{
    ValueTask IMessageCreateGatewayHandler.HandleAsync(Message message)
    {
        if (!IsWrittenByAPerson(message.Type))
        {
            return ValueTask.CompletedTask;
        }

        return mapper.ForwardAsync(nameof(GatewayClient.MessageCreate), message.GuildId, guild =>
        {
            var reply = message.Type == MessageType.Reply ? message.MessageReference : null;
            var mentioned = MentionedUsers(message);
            return new PlatformMessageCreated(
                guild,
                message.CreatedAt,
                DiscordIds.ToRef(message.ChannelId),
                DiscordIds.ToRef(message.Id),
                DiscordIds.ToRef(message.Author.Id),
                message.Author.IsBot,
                reply is null ? null : DiscordIds.ToRef(reply.MessageId),
                reply is null || message.ReferencedMessage is not { } referenced ? null : DiscordIds.ToRef(referenced.Author.Id),
                [.. mentioned.Select(DiscordIds.ToRef)],
                ToMetadata(message, mentioned.Count));
        }, CancellationToken.None);
    }

    ValueTask IMessageUpdateGatewayHandler.HandleAsync(Message message)
    {
        if (!IsWrittenByAPerson(message.Type) || message.EditedAt is not { } editedAt)
        {
            return ValueTask.CompletedTask;
        }

        return mapper.ForwardAsync(nameof(GatewayClient.MessageUpdate), message.GuildId, guild =>
            new PlatformMessageUpdated(
                guild,
                editedAt,
                DiscordIds.ToRef(message.ChannelId),
                DiscordIds.ToRef(message.Id),
                DiscordIds.ToRef(message.Author.Id),
                ToMetadata(message, MentionedUsers(message).Count)), CancellationToken.None);
    }

    public ValueTask HandleAsync(MessageDeleteEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.MessageDelete), arg.GuildId, guild =>
            Deleted(guild, arg.ChannelId, arg.MessageId), CancellationToken.None);

    public async ValueTask HandleAsync(MessageDeleteBulkEventArgs arg)
    {
        foreach (var messageId in arg.MessageIds)
        {
            await mapper.ForwardAsync(nameof(GatewayClient.MessageDeleteBulk), arg.GuildId, guild =>
                Deleted(guild, arg.ChannelId, messageId), CancellationToken.None);
        }
    }

    private static bool IsWrittenByAPerson(MessageType type) =>
        type is MessageType.Default or MessageType.Reply or MessageType.ApplicationCommand or MessageType.ContextMenuCommand;

    /// <summary>
    /// The distinct users the text mentions. A reply that pings its author lists that author in <c>mentions</c> even
    /// when the text doesn't mention them; that ping is not a mention.
    /// </summary>
    private static List<ulong> MentionedUsers(Message message)
    {
        var mentioned = message.MentionedUsers.Select(user => user.Id).Distinct().ToList();
        if (message.Type == MessageType.Reply
            && message.ReferencedMessage?.Author.Id is { } repliedTo
            && !MentionsInText(message.Content, repliedTo))
        {
            mentioned.Remove(repliedTo);
        }

        return mentioned;
    }

    private static bool MentionsInText(string content, ulong userId)
    {
        var id = userId.ToString(CultureInfo.InvariantCulture);
        return content.Contains($"<@{id}>", StringComparison.Ordinal) || content.Contains($"<@!{id}>", StringComparison.Ordinal);
    }

    private static MessageMetadata ToMetadata(Message message, int userMentionCount) =>
        MessageMetadata.From(
            message.Content,
            message.Attachments.Count,
            message.Embeds.Count,
            userMentionCount,
            message.MentionedRoleIds.Distinct().Count(),
            message.MentionEveryone);

    private PlatformMessageDeleted Deleted(PlatformRef guild, ulong channelId, ulong messageId) =>
        new(guild, timeProvider.GetUtcNow(), DiscordIds.ToRef(channelId), DiscordIds.ToRef(messageId), Author: null);
}
