using System.Runtime.CompilerServices;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;

namespace Apollo.Platform.Discord;

internal sealed class DiscordChatPlatform(
    GatewayClient gateway,
    DiscordMemberDownloader memberDownloader,
    IOptions<DiscordPlatformOptions> options) : IChatPlatform
{
    private readonly ulong _guildId = options.Value.GuildId
        ?? throw new InvalidOperationException($"{DiscordPlatformOptions.SectionName}:GuildId is not configured.");

    public PlatformKind Platform => PlatformKind.Discord;

    public PlatformCapabilities Capabilities { get; } = new(PresenceActivities: true);

    public Task<PlatformMember?> GetMemberAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken)
    {
        var (cached, guildUser) = CachedMember(guild, user);
        return Task.FromResult(guildUser is null ? null : DiscordSnapshots.ToMember(guildUser, cached!));
    }

    public async Task<IReadOnlyList<PlatformRef>> GetMemberRolesAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken) =>
        (await GetMemberAsync(guild, user, cancellationToken))?.Roles ?? [];

    public async Task<PlatformPermissions> GetMemberPermissionsAsync(PlatformRef guild, PlatformRef user, CancellationToken cancellationToken) =>
        await GetMemberAsync(guild, user, cancellationToken) is { IsAdministrator: true }
            ? PlatformPermissions.Administrator
            : PlatformPermissions.None;

    /// <summary>
    /// Every role except @everyone, which every member holds implicitly (like <see cref="PlatformMember.Roles"/>).
    /// </summary>
    public Task<IReadOnlyList<PlatformRole>> ListRolesAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        FromCache(guild, cached => DiscordSnapshots.RolesWithoutEveryone(cached).Select(DiscordSnapshots.ToRole));

    public Task<IReadOnlyList<PlatformChannel>> ListChannelsAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        FromCache(guild, cached => cached.Channels.Values
            .Concat(cached.ActiveThreads.Values)
            .Select(DiscordSnapshots.ToChannel));

    /// <summary>
    /// Requests the full member list over the gateway, because GUILD_CREATE lists only part of the members of a large
    /// guild. Takes a gateway round trip per call; empty until the guild's GUILD_CREATE arrived.
    /// </summary>
    /// <exception cref="TimeoutException">The member list did not arrive in time (<see cref="DiscordMemberDownloader.Timeout"/>).</exception>
    public async Task<IReadOnlyList<PlatformMember>> ListMembersAsync(PlatformRef guild, CancellationToken cancellationToken)
    {
        if (CachedGuild(guild) is null)
        {
            return [];
        }

        var users = await memberDownloader.DownloadAsync(_guildId, cancellationToken);

        // Read the guild again: its roles (for the permissions) may have changed during the download.
        var cached = CachedGuild(guild);
        return [.. users.Select(user => DiscordSnapshots.ToMember(user, cached))];
    }

    public Task<IReadOnlyList<PlatformVoiceState>> ListVoiceStatesAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        FromCache(guild, cached => cached.VoiceStates.Values.Select(DiscordSnapshots.ToVoiceState));

    public Task<IReadOnlyList<PlatformPresence>> ListPresencesAsync(PlatformRef guild, CancellationToken cancellationToken) =>
        FromCache(guild, cached => cached.Presences.Values.Select(DiscordSnapshots.ToPresence));

    public Task AddRoleAsync(PlatformRef guild, PlatformRef user, PlatformRef role, CancellationToken cancellationToken) =>
        gateway.Rest.AddGuildUserRoleAsync(
            ConfiguredGuild(guild), DiscordIds.ToSnowflake(user), DiscordIds.ToSnowflake(role), cancellationToken: cancellationToken);

    public Task RemoveRoleAsync(PlatformRef guild, PlatformRef user, PlatformRef role, CancellationToken cancellationToken) =>
        gateway.Rest.RemoveGuildUserRoleAsync(
            ConfiguredGuild(guild), DiscordIds.ToSnowflake(user), DiscordIds.ToSnowflake(role), cancellationToken: cancellationToken);

    /// <summary>
    /// The guild from the gateway cache; <see langword="null"/> until its GUILD_CREATE arrived.
    /// </summary>
    private Guild? CachedGuild(PlatformRef guild, [CallerArgumentExpression(nameof(guild))] string? parameterName = null) =>
        gateway.Cache.Guilds.GetValueOrDefault(ConfiguredGuild(guild, parameterName));

    private Task<IReadOnlyList<T>> FromCache<T>(
        PlatformRef guild,
        Func<Guild, IEnumerable<T>> read,
        [CallerArgumentExpression(nameof(guild))] string? parameterName = null) =>
        Task.FromResult<IReadOnlyList<T>>(CachedGuild(guild, parameterName) is { } cached ? [.. read(cached)] : []);

    private (Guild? Guild, GuildUser? User) CachedMember(PlatformRef guild, PlatformRef user)
    {
        var cached = CachedGuild(guild);
        var userId = DiscordIds.ToSnowflake(user);
        return (cached, cached is not null && cached.Users.TryGetValue(userId, out var guildUser) ? guildUser : null);
    }

    /// <summary>
    /// The bot serves only the configured guild (scope §4.3); a call for any other guild is a bug in the caller.
    /// </summary>
    private ulong ConfiguredGuild(PlatformRef guild, [CallerArgumentExpression(nameof(guild))] string? parameterName = null)
    {
        var guildId = DiscordIds.ToSnowflake(guild, parameterName);
        return guildId == _guildId
            ? guildId
            : throw new ArgumentException($"{guild} is not the configured guild.", parameterName);
    }
}
