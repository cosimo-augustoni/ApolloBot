using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary><see cref="IAppInfoClient"/> against the same-origin API (<c>GET /api/app-info</c>).</summary>
public sealed class HttpAppInfoClient(HttpClient http) : IAppInfoClient
{
    public const string Path = "api/app-info";

    public async Task<AppInfoDto> GetAppInfoAsync(CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<AppInfoDto>(Path, cancellationToken)
            ?? throw new InvalidOperationException($"GET {Path} returned no content.");
}
