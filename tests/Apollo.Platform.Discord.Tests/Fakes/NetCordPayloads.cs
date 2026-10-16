using System.Text.Json;
using System.Text.Json.Serialization;
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
