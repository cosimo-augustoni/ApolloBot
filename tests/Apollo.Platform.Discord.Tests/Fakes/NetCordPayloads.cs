using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private static readonly DateTimeOffset _discordEpoch = new(2015, 1, 1, 0, 0, 0, TimeSpan.Zero);

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

    /// <summary>
    /// A guild message as MESSAGE_CREATE / MESSAGE_UPDATE deliver it.
    /// </summary>
    /// <param name="guildId">The guild.</param>
    /// <param name="id">The message id (a snowflake, so it also gives the creation time).</param>
    /// <param name="authorId">The author.</param>
    /// <param name="content">The text.</param>
    /// <param name="channelId">The channel or thread.</param>
    /// <param name="type">The message type (0 default, 19 reply, 7 member join, ...).</param>
    /// <param name="bot">Whether the author is a bot.</param>
    /// <param name="mentions">The ids in the payload's <c>mentions</c>.</param>
    /// <param name="mentionRoles">The ids in the payload's <c>mention_roles</c>.</param>
    /// <param name="mentionEveryone">The payload's <c>mention_everyone</c>.</param>
    /// <param name="attachments">How many attachments.</param>
    /// <param name="embeds">How many embeds.</param>
    /// <param name="replyTo">For a reply: the replied-to message and its author (<c>null</c> author: no <c>referenced_message</c>).</param>
    /// <param name="forwardOf">For a forward: the forwarded message.</param>
    /// <param name="editedAt">The payload's <c>edited_timestamp</c>.</param>
    public static Message GuildMessage(
        ulong guildId,
        ulong id,
        ulong authorId,
        string content = "hello",
        ulong channelId = 500,
        int type = 0,
        bool bot = false,
        ulong[]? mentions = null,
        ulong[]? mentionRoles = null,
        bool mentionEveryone = false,
        int attachments = 0,
        int embeds = 0,
        (ulong MessageId, ulong? AuthorId)? replyTo = null,
        ulong? forwardOf = null,
        DateTimeOffset? editedAt = null)
    {
        var json = new JsonObject
        {
            ["id"] = id.ToString(CultureInfo.InvariantCulture),
            ["channel_id"] = channelId.ToString(CultureInfo.InvariantCulture),
            ["guild_id"] = guildId.ToString(CultureInfo.InvariantCulture),
            ["type"] = type,
            ["content"] = content,
            ["timestamp"] = SnowflakeTime(id).ToString("O", CultureInfo.InvariantCulture),
            ["edited_timestamp"] = editedAt?.ToString("O", CultureInfo.InvariantCulture),
            ["author"] = new JsonObject { ["id"] = authorId.ToString(CultureInfo.InvariantCulture), ["username"] = "author", ["bot"] = bot },
            ["tts"] = false,
            ["pinned"] = false,
            ["mention_everyone"] = mentionEveryone,
            ["mentions"] = new JsonArray([.. (mentions ?? []).Select(m => (JsonNode)new JsonObject { ["id"] = m.ToString(CultureInfo.InvariantCulture), ["username"] = "user" })]),
            ["mention_roles"] = new JsonArray([.. (mentionRoles ?? []).Select(r => (JsonNode)r.ToString(CultureInfo.InvariantCulture))]),
            ["attachments"] = new JsonArray([.. Enumerable.Range(1, attachments).Select(a => (JsonNode)new JsonObject
            {
                ["id"] = a.ToString(CultureInfo.InvariantCulture), ["filename"] = "file.png", ["size"] = 1, ["url"] = "https://cdn.example.test/file.png", ["proxy_url"] = "https://cdn.example.test/file.png",
            })]),
            ["embeds"] = new JsonArray([.. Enumerable.Range(1, embeds).Select(_ => (JsonNode)new JsonObject { ["type"] = "rich" })]),
        };

        if (replyTo is { } reply)
        {
            json["message_reference"] = new JsonObject { ["type"] = 0, ["message_id"] = reply.MessageId.ToString(CultureInfo.InvariantCulture), ["channel_id"] = channelId.ToString(CultureInfo.InvariantCulture) };
            if (reply.AuthorId is { } replyAuthor)
            {
                json["referenced_message"] = JsonNode.Parse(GuildMessageJson(guildId, reply.MessageId, replyAuthor, channelId));
            }
        }

        if (forwardOf is { } forwarded)
        {
            json["message_reference"] = new JsonObject { ["type"] = 1, ["message_id"] = forwarded.ToString(CultureInfo.InvariantCulture), ["channel_id"] = channelId.ToString(CultureInfo.InvariantCulture) };
        }

        return new(Parse<JsonMessage>(json.ToJsonString()), null, null, Rest);
    }

    public static MessageDeleteEventArgs MessageDelete(ulong guildId, ulong messageId, ulong channelId = 500) =>
        new(new JsonMessageDeleteEventArgs { GuildId = guildId, ChannelId = channelId, MessageId = messageId });

    public static MessageDeleteBulkEventArgs MessageDeleteBulk(ulong guildId, ulong channelId, params ulong[] messageIds) =>
        new(new JsonMessageDeleteBulkEventArgs { GuildId = guildId, ChannelId = channelId, MessageIds = messageIds });

    /// <summary>
    /// The snowflake Discord would give an object created at <paramref name="createdAt"/>.
    /// </summary>
    public static ulong Snowflake(DateTimeOffset createdAt, ulong sequence = 1) =>
        ((ulong)(createdAt - _discordEpoch).TotalMilliseconds << 22) | sequence;

    private static DateTimeOffset SnowflakeTime(ulong id) => _discordEpoch.AddMilliseconds(id >> 22);

    private static string GuildMessageJson(ulong guildId, ulong id, ulong authorId, ulong channelId) => $$"""
        {
          "id": "{{id}}", "channel_id": "{{channelId}}", "guild_id": "{{guildId}}", "type": 0, "content": "earlier",
          "timestamp": "{{SnowflakeTime(id):O}}", "author": { "id": "{{authorId}}", "username": "author" },
          "tts": false, "pinned": false, "mention_everyone": false,
          "mentions": [], "mention_roles": [], "attachments": [], "embeds": []
        }
        """;

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
