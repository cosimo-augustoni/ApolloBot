using NetCord;
using NetCord.Gateway.JsonModels;
using NetCord.JsonModels;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// Builds the guild NetCord caches from GUILD_CREATE, from NetCord's JSON models. Starts with only the @everyone role.
/// </summary>
internal sealed class DiscordGuildBuilder(ulong guildId = DiscordHost.GuildId)
{
    private readonly List<JsonRole> _roles = [new() { Id = guildId, Name = "@everyone", Colors = new() }];
    private readonly List<JsonGuildUser> _members = [];
    private readonly List<JsonChannel> _channels = [];
    private readonly List<JsonChannel> _threads = [];
    private readonly List<JsonVoiceState> _voiceStates = [];
    private readonly List<JsonPresence> _presences = [];
    private ulong _ownerId = 1;

    public DiscordGuildBuilder OwnedBy(ulong userId)
    {
        _ownerId = userId;
        return this;
    }

    public DiscordGuildBuilder WithRole(ulong id, string name, int position = 1, Permissions permissions = default, bool managed = false, int color = 0)
    {
        _roles.Add(NetCordPayloads.JsonRole(id, name, position, permissions, managed, color));
        return this;
    }

    public DiscordGuildBuilder WithMember(
        ulong userId,
        string username = "alice",
        string? globalName = null,
        string? nickname = null,
        ulong[]? roles = null,
        bool bot = false,
        DateTimeOffset? joinedAt = null,
        string? avatarHash = null)
    {
        _members.Add(NetCordPayloads.JsonMember(userId, username, globalName, nickname, roles, bot, joinedAt, avatarHash));
        return this;
    }

    public DiscordGuildBuilder WithChannel(ulong id, string name, ChannelType type, ulong? parentId = null)
    {
        var channel = NetCordPayloads.JsonChannel(guildId, id, name, type, parentId);
        if (type is ChannelType.PublicGuildThread or ChannelType.PrivateGuildThread or ChannelType.AnnouncementGuildThread)
        {
            _threads.Add(channel);
        }
        else
        {
            _channels.Add(channel);
        }

        return this;
    }

    public DiscordGuildBuilder WithVoiceState(
        ulong userId,
        ulong? channelId,
        bool selfMute = false,
        bool selfDeaf = false,
        bool streaming = false,
        bool video = false)
    {
        _voiceStates.Add(new JsonVoiceState
        {
            GuildId = guildId,
            UserId = userId,
            ChannelId = channelId,
            SessionId = $"session-{userId}",
            IsSelfMuted = selfMute,
            IsSelfDeafened = selfDeaf,
            SelfStreamExists = streaming,
            SelfVideoExists = video,
        });
        return this;
    }

    public DiscordGuildBuilder WithPresence(ulong userId, UserStatusType status, params JsonUserActivity[] activities)
    {
        _presences.Add(new JsonPresence { User = new JsonUser { Id = userId }, GuildId = guildId, Status = status, Activities = activities });
        return this;
    }

    public JsonGuild Build() => new()
    {
        Id = guildId,
        Name = "Apollo test guild",
        OwnerId = _ownerId,
        Roles = [.. _roles],
        Emojis = [],
        Features = [],
        Stickers = [],
        Users = [.. _members],
        Channels = [.. _channels],
        ActiveThreads = [.. _threads],
        VoiceStates = [.. _voiceStates],
        Presences = [.. _presences],
        StageInstances = [],
        ScheduledEvents = [],
    };
}
