using System.Text.Json;
using System.Text.Json.Serialization;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.JsonModels.EventArgs;
using NetCord.JsonModels;
using NetCord.Rest;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// Hand-built NetCord payloads (guidelines §4: no live Discord in tests), made from NetCord's own JSON models. Payloads
/// with many required fields are parsed from gateway JSON, the way Discord sends them.
/// </summary>
internal static class NetCordPayloads
{
    public static readonly RestClient Rest = new();

    private static readonly JsonSerializerOptions _gatewayJson = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public static JsonRole JsonRole(ulong id, string name, int position = 1, Permissions permissions = default, bool managed = false, int color = 0) =>
        new() { Id = id, Name = name, Position = position, Permissions = permissions, Managed = managed, Colors = new() { PrimaryColor = new Color(color) } };

    public static JsonGuildUser JsonMember(
        ulong userId,
        string username = "alice",
        string? globalName = null,
        string? nickname = null,
        ulong[]? roles = null,
        bool bot = false,
        DateTimeOffset? joinedAt = null,
        string? avatarHash = null) => new()
        {
            User = new JsonUser { Id = userId, Username = username, GlobalName = globalName, IsBot = bot, AvatarHash = avatarHash },
            Nickname = nickname,
            RoleIds = roles ?? [],
            JoinedAt = joinedAt,
        };

    public static JsonChannel JsonChannel(ulong guildId, ulong id, string name, ChannelType type, ulong? parentId = null) =>
        new() { Id = id, Name = name, Type = type, GuildId = guildId, ParentId = parentId, PermissionOverwrites = [] };

    public static GuildUser Member(ulong guildId, ulong userId, string username = "alice", ulong[]? roles = null) =>
        new(JsonMember(userId, username, roles: roles), guildId, Rest);

    public static Role Role(ulong guildId, ulong id, string name, int position = 1, Permissions permissions = default, int color = 0) =>
        new(JsonRole(id, name, position, permissions, color: color), guildId, Rest);

    public static RoleDeleteEventArgs RoleDelete(ulong guildId, ulong roleId) =>
        new(new JsonRoleDeleteEventArgs { GuildId = guildId, RoleId = roleId });

    public static IGuildChannel Channel(ulong guildId, ulong id, string name, ChannelType type, ulong? parentId = null) =>
        IGuildChannel.CreateFromJson(JsonChannel(guildId, id, name, type, parentId), guildId, Rest);

    /// <summary>
    /// A thread as NetCord builds it from a guild's active threads (threads need more than a plain channel payload).
    /// </summary>
    public static GuildThread Thread(ulong guildId, ulong id, string name, ulong parentId)
    {
        using var gateway = new TestGateway(new DiscordGuildBuilder(guildId).WithChannel(id, name, ChannelType.PublicGuildThread, parentId).Build());
        return gateway.Client.Cache.Guilds[guildId].ActiveThreads[id];
    }

    public static GuildUserChunkEventArgs Chunk(ulong guildId, string? nonce, int index, int count, params JsonGuildUser[] members) =>
        new(new JsonGuildUserChunkEventArgs { GuildId = guildId, Nonce = nonce!, ChunkIndex = index, ChunkCount = count, Users = members, NotFound = [] }, Rest);

    public static GuildUserRemoveEventArgs GuildUserRemove(ulong guildId, ulong userId) =>
        new(new JsonGuildUserRemoveEventArgs { GuildId = guildId, User = new JsonUser { Id = userId, Username = "alice" } }, Rest);

    public static Message DirectMessage(ulong authorId) => new(Parse<JsonMessage>($$"""
        {
          "id": "1", "channel_id": "2", "type": 0, "content": "hi", "timestamp": "2026-01-15T09:00:00+00:00",
          "author": { "id": "{{authorId}}", "username": "alice" },
          "tts": false, "pinned": false, "mention_everyone": false,
          "mentions": [], "mention_roles": [], "attachments": [], "embeds": []
        }
        """), null, null, Rest);

    public static ReadyEventArgs Ready() => new(Parse<JsonReadyEventArgs>("""
        {
          "v": 10, "session_id": "session", "resume_gateway_url": "wss://gateway.example.test",
          "user": { "id": "999", "username": "apollo", "bot": true },
          "guilds": [ { "id": "111111111111111111", "unavailable": true } ],
          "application": { "id": "999", "flags": 0 }
        }
        """), Rest);

    private static T Parse<T>(string json) => JsonSerializer.Deserialize<T>(json, _gatewayJson)!;
}
