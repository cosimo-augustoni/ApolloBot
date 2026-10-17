using Apollo.Platform.Abstractions.Events;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps PRESENCE_UPDATE to <see cref="PlatformPresenceUpdated"/> (#19): the user's status and every activity with type
/// and name, custom statuses included (the recorder ignores them). Discord sends no timestamp, so the event is stamped
/// on receipt. The current presences on connect come from the guild cache (<c>ListPresencesAsync</c>).
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordPresenceHandler(DiscordEventMapper mapper, TimeProvider timeProvider) : IPresenceUpdateGatewayHandler
{
    public ValueTask HandleAsync(Presence arg) =>
        mapper.ForwardAsync(nameof(GatewayClient.PresenceUpdate), arg.GuildId, guild =>
            new PlatformPresenceUpdated(guild, timeProvider.GetUtcNow(), DiscordSnapshots.ToPresence(arg)), CancellationToken.None);
}
