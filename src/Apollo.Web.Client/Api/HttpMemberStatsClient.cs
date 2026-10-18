using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary><see cref="IMemberStatsClient"/> against the same-origin API (<c>GET /api/admin/members/{userId}/stats</c>).</summary>
public sealed class HttpMemberStatsClient(HttpClient http) : IMemberStatsClient
{
    public const string Path = "api/admin/members";

    public async Task<MemberStatsDto?> GetAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var uri = new Uri($"{Path}/{userId}/stats?from={Day(from)}&to={Day(to)}", UriKind.Relative);
        using var response = await http.GetAsync(uri, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MemberStatsDto>(cancellationToken)
            ?? throw new InvalidOperationException($"GET {Path}/{userId}/stats returned no content.");
    }

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
