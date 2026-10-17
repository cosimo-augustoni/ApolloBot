using Apollo.Platform.Abstractions.Events;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Maps GUILD_CREATE (on every new session, so also after a reconnect) to <see cref="PlatformGuildAvailable"/>, the
/// snapshot the guild state sync (#13) reconciles against.
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed class DiscordGuildHandler(DiscordEventMapper mapper, GatewayClient gateway, TimeProvider timeProvider) : IGuildCreateGatewayHandler
{
    public ValueTask HandleAsync(GuildCreateEventArgs arg)
    {
        // An unavailable guild (outage) comes without data; the real snapshot follows once it is back.
        if (arg.Guild is not { } guild)
        {
            return default;
        }

        return mapper.ForwardAsync(nameof(GatewayClient.GuildCreate), arg.GuildId, guildRef => new PlatformGuildAvailable(
            Guild: guildRef,
            OccurredAt: timeProvider.GetUtcNow(),
            Name: guild.Name,
            Owner: DiscordIds.ToRef(guild.OwnerId),
            Bot: DiscordIds.ToRef(gateway.Id),
            Roles: [.. DiscordSnapshots.RolesWithoutEveryone(guild).Select(DiscordSnapshots.ToRole)],
            Channels: [.. guild.Channels.Values.Concat(guild.ActiveThreads.Values).Select(DiscordSnapshots.ToChannel)],
            Members: [.. guild.Users.Values.Select(user => DiscordSnapshots.ToMember(user, guild))],
            VoiceStates: [.. guild.VoiceStates.Values.Select(DiscordSnapshots.ToVoiceState)],
            Presences: [.. guild.Presences.Values.Select(DiscordSnapshots.ToPresence)]),
            CancellationToken.None);
    }
}
