using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary><see cref="IBotStatusClient"/> against the same-origin API (<c>GET /api/admin/bot-status</c>).</summary>
public sealed class HttpBotStatusClient(HttpClient http) : IBotStatusClient
{
    public const string Path = "api/admin/bot-status";

    public async Task<BotStatusDto> GetStatusAsync(CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<BotStatusDto>(new Uri(Path, UriKind.Relative), cancellationToken)
            ?? throw new InvalidOperationException($"GET {Path} returned no content.");
}
