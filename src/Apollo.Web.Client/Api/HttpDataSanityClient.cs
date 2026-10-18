using System.Globalization;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary><see cref="IDataSanityClient"/> against the same-origin API (<c>GET /api/admin/sanity</c>).</summary>
public sealed class HttpDataSanityClient(HttpClient http) : IDataSanityClient
{
    public const string Path = "api/admin/sanity";

    public async Task<DataSanityDto> GetAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var uri = new Uri(from is { } start && to is { } end ? $"{Path}?from={Day(start)}&to={Day(end)}" : Path, UriKind.Relative);
        return await http.GetFromJsonAsync<DataSanityDto>(uri, cancellationToken)
            ?? throw new InvalidOperationException($"GET {Path} returned no content.");
    }

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
