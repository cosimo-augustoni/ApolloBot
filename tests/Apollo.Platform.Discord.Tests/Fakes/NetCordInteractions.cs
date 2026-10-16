using System.Text.Json;
using System.Text.Json.Serialization;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// Hand-built slash-command and button interactions, parsed from gateway JSON. The initial response goes to
/// <see cref="Responses"/> instead of Discord; later REST calls (editing the response) go to <see cref="Rest"/>.
/// </summary>
internal sealed class NetCordInteractions : IDisposable
{
    public const ulong ApplicationId = 999;

    public const string Token = "interaction-token";

    /// <summary>
    /// What Discord answers to an edit of the interaction response.
    /// </summary>
    public const string ResponseMessageJson = """
        {
          "id": "8", "channel_id": "2", "type": 20, "content": "Fertig.", "timestamp": "2026-01-15T09:00:00+00:00",
          "author": { "id": "999", "username": "apollo", "bot": true },
          "tts": false, "pinned": false, "mention_everyone": false,
          "mentions": [], "mention_roles": [], "attachments": [], "embeds": [], "flags": 64
        }
        """;

    private static readonly JsonSerializerOptions _gatewayJson = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    private readonly RestClient _rest;

    public NetCordInteractions() => _rest = new RestClient(new RestClientConfiguration { RequestHandler = Rest });

    public RecordingRestRequestHandler Rest { get; } = new();

    /// <summary>
    /// Every initial interaction response, in order.
    /// </summary>
    public List<InteractionCallbackProperties> Responses { get; } = [];

    /// <summary>
    /// A slash command; <paramref name="options"/> is the JSON array of the <c>data.options</c>.
    /// </summary>
    public SlashCommandInteraction SlashCommand(ulong? guildId, ulong userId, string name, string options = "[]") =>
        new(Parse(guildId, userId, type: 2, $$"""{ "id": "5", "name": "{{name}}", "type": 1, "options": {{options}} }""", message: null),
            null, RespondAsync, _rest);

    public ButtonInteraction ButtonClick(ulong? guildId, ulong userId, string customId) =>
        new(Parse(guildId, userId, type: 3, $$"""{ "custom_id": "{{customId}}", "component_type": 2 }""", message: """
            {
              "id": "7", "channel_id": "2", "type": 0, "content": "Wirklich?", "timestamp": "2026-01-15T09:00:00+00:00",
              "author": { "id": "999", "username": "apollo", "bot": true },
              "tts": false, "pinned": false, "mention_everyone": false,
              "mentions": [], "mention_roles": [], "attachments": [], "embeds": [], "flags": 64
            }
            """), null, RespondAsync, _rest);

    public void Dispose() => _rest.Dispose();

    private static JsonInteraction Parse(ulong? guildId, ulong userId, int type, string data, string? message)
    {
        var user = $$"""{ "id": "{{userId}}", "username": "alice" }""";
        var origin = guildId is { } id
            ? $$"""
                "guild_id": "{{id}}", "context": 0,
                "member": { "user": {{user}}, "roles": [], "joined_at": "2026-01-01T00:00:00+00:00", "deaf": false, "mute": false, "flags": 0 },
                """
            : $$""" "context": 1, "user": {{user}}, """;
        var json = $$"""
            {
              "id": "1300000000000000000", "application_id": "{{ApplicationId}}", "type": {{type}}, "data": {{data}},
              {{origin}}
              "channel_id": "2", "channel": { "id": "2", "type": 0 },
              {{(message is null ? string.Empty : $"\"message\": {message},")}}
              "token": "{{Token}}", "version": 1, "locale": "de", "entitlements": [],
              "authorizing_integration_owners": {}, "attachment_size_limit": 0
            }
            """;
        return JsonSerializer.Deserialize<JsonInteraction>(json, _gatewayJson)!;
    }

    private Task<InteractionCallbackResponse?> RespondAsync(
        IInteraction interaction,
        InteractionCallbackProperties callback,
        bool withResponse,
        RestRequestProperties? properties,
        CancellationToken cancellationToken)
    {
        Responses.Add(callback);
        return Task.FromResult<InteractionCallbackResponse?>(null);
    }
}
