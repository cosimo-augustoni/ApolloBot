using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps MESSAGE_REACTION_ADD / REMOVE to <see cref="PlatformReactionAdded"/> / <see cref="PlatformReactionRemoved"/>
/// (#17). Discord sends no timestamp, so the events are stamped on receipt. Only the add payload carries the message
/// author; the recorder looks it up for removes.
/// </summary>
/// <remarks>
/// <para>
/// REMOVE_ALL and REMOVE_EMOJI are moderator bulk removals, not user actions, and are not mapped. Super reactions
/// (<c>burst</c>) are mapped like normal ones.
/// </para>
/// <para>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</para>
/// </remarks>
internal sealed class DiscordReactionHandler(DiscordEventMapper mapper, TimeProvider timeProvider)
    : IMessageReactionAddGatewayHandler, IMessageReactionRemoveGatewayHandler
{
    public ValueTask HandleAsync(MessageReactionAddEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.MessageReactionAdd), arg.GuildId, guild =>
            new PlatformReactionAdded(
                guild,
                timeProvider.GetUtcNow(),
                DiscordIds.ToRef(arg.ChannelId),
                DiscordIds.ToRef(arg.MessageId),
                DiscordIds.ToRef(arg.UserId),
                arg.MessageAuthorId is { } author ? DiscordIds.ToRef(author) : null,
                ToEmoji(arg.Emoji)), CancellationToken.None);

    public ValueTask HandleAsync(MessageReactionRemoveEventArgs arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.MessageReactionRemove), arg.GuildId, guild =>
            new PlatformReactionRemoved(
                guild,
                timeProvider.GetUtcNow(),
                DiscordIds.ToRef(arg.ChannelId),
                DiscordIds.ToRef(arg.MessageId),
                DiscordIds.ToRef(arg.UserId),
                MessageAuthor: null,
                ToEmoji(arg.Emoji)), CancellationToken.None);

    /// <summary>
    /// A unicode emoji has no id and its name is the emoji itself. Discord omits a custom emoji's name when the emoji is
    /// no longer available; the id still identifies it, so the name is left empty.
    /// </summary>
    private static PlatformEmoji ToEmoji(MessageReactionEmoji emoji) =>
        new(emoji.Name ?? string.Empty, emoji.Id is { } id ? DiscordIds.ToRef(id) : null);
}
