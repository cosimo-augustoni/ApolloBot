using System.Globalization;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary><see cref="IServerStatsClient"/> against the same-origin API (<c>GET /api/admin/stats/server</c>).</summary>
public sealed class HttpServerStatsClient(HttpClient http) : IServerStatsClient
{
    public const string Path = "api/admin/stats/server";

    public async Task<ActivityStatsDto> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var uri = new Uri($"{Path}?from={Day(from)}&to={Day(to)}", UriKind.Relative);
        return await http.GetFromJsonAsync<ActivityStatsDto>(uri, cancellationToken)
            ?? throw new InvalidOperationException($"GET {Path} returned no content.");
    }

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
