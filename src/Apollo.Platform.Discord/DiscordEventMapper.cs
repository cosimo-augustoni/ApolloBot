using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord;

/// <summary>
/// The one path from NetCord gateway events to <see cref="IPlatformEventDispatcher"/>. It holds the single-guild filter
/// (scope §4.3), so every event mapper (#13, #15-#19) goes through it instead of filtering on its own.
/// </summary>
internal sealed partial class DiscordEventMapper(
    IOptions<DiscordPlatformOptions> options,
    IPlatformEventDispatcher dispatcher,
    ILogger<DiscordEventMapper> logger)
{
    private readonly ulong _guildId = options.Value.GuildId
        ?? throw new InvalidOperationException($"{DiscordPlatformOptions.SectionName}:GuildId is not configured.");

    /// <summary>
    /// The configured guild.
    /// </summary>
    public PlatformRef Guild => field ??= DiscordIds.ToRef(_guildId);

    /// <summary>
    /// Maps and dispatches an event of the configured guild. Events of any other guild, and guild-less events such as
    /// DMs, are dropped before <paramref name="map"/> runs.
    /// </summary>
    /// <param name="gatewayEvent">The NetCord event name, for the log.</param>
    /// <param name="guildId">The event's guild; <see langword="null"/> for DMs and other guild-less events.</param>
    /// <param name="map">Builds the neutral event for the configured guild.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    public async ValueTask ForwardAsync(
        string gatewayEvent,
        ulong? guildId,
        Func<PlatformRef, PlatformEvent> map,
        CancellationToken cancellationToken)
    {
        if (IsConfiguredGuild(gatewayEvent, guildId))
        {
            await dispatcher.DispatchAsync(map(Guild), cancellationToken);
        }
    }

    /// <summary>
    /// The single-guild filter: whether <paramref name="guildId"/> is the configured guild. Logs why anything else is
    /// dropped. Command and button interactions use it too, before they reach the command router.
    /// </summary>
    /// <param name="gatewayEvent">The NetCord event name, for the log.</param>
    /// <param name="guildId">The event's guild; <see langword="null"/> for DMs and other guild-less events.</param>
    public bool IsConfiguredGuild(string gatewayEvent, ulong? guildId)
    {
        if (guildId is null)
        {
            LogDroppedGuildless(gatewayEvent);
            return false;
        }

        if (guildId != _guildId)
        {
            LogDroppedForeignGuild(gatewayEvent, guildId.Value);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Dispatches a connection state change. Connection events have no guild, so they are reported for the configured one.
    /// </summary>
    public async ValueTask ForwardConnectionStateAsync(
        PlatformConnectionState state,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken) =>
        await dispatcher.DispatchAsync(new PlatformConnectionStateChanged(Guild, occurredAt, state), cancellationToken);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {GatewayEvent} from guild {GuildId}: not the configured guild")]
    private partial void LogDroppedForeignGuild(string gatewayEvent, ulong guildId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {GatewayEvent}: not from a guild")]
    private partial void LogDroppedGuildless(string gatewayEvent);
}
