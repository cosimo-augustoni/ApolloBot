using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary><see cref="IDataOverviewClient"/> against the same-origin API (<c>/api/admin/data</c>).</summary>
public sealed class HttpDataOverviewClient(HttpClient http) : IDataOverviewClient
{
    public const string BasePath = "api/admin/data";

    public Task<EventCountsDto> GetCountsAsync(CancellationToken cancellationToken) =>
        GetAsync<EventCountsDto>($"{BasePath}/counts", cancellationToken);

    public async Task<IReadOnlyList<MemberTrackingDto>> GetMemberTrackingAsync(CancellationToken cancellationToken) =>
        await GetAsync<MemberTrackingDto[]>($"{BasePath}/members", cancellationToken);

    public Task<RawEventPageDto> GetEventsAsync(RawEventFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = new List<string>
        {
            $"type={JsonNamingPolicy.CamelCase.ConvertName(filter.Type.ToString())}",
            $"page={filter.Page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={filter.PageSize.ToString(CultureInfo.InvariantCulture)}",
        };
        if (filter.UserId is { } userId)
        {
            query.Add($"userId={userId}");
        }

        if (filter.ChannelId is { } channelId)
        {
            query.Add($"channelId={channelId}");
        }

        if (filter.From is { } from)
        {
            query.Add($"from={Instant(from)}");
        }

        if (filter.To is { } to)
        {
            query.Add($"to={Instant(to)}");
        }

        return GetAsync<RawEventPageDto>($"{BasePath}/events?{string.Join('&', query)}", cancellationToken);
    }

    public async Task<IReadOnlyList<EventUserDto>> SearchUsersAsync(string search, CancellationToken cancellationToken) =>
        await GetAsync<EventUserDto[]>($"{BasePath}/users?search={Uri.EscapeDataString(search ?? string.Empty)}", cancellationToken);

    public async Task<IReadOnlyList<EventChannelDto>> SearchChannelsAsync(string search, CancellationToken cancellationToken) =>
        await GetAsync<EventChannelDto[]>($"{BasePath}/channels?search={Uri.EscapeDataString(search ?? string.Empty)}", cancellationToken);

    /// <summary>Round-trip format with the offset escaped, so a <c>+</c> does not turn into a space.</summary>
    private static string Instant(DateTimeOffset instant) => Uri.EscapeDataString(instant.ToString("O", CultureInfo.InvariantCulture));

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<T>(new Uri(path, UriKind.Relative), cancellationToken)
            ?? throw new InvalidOperationException($"GET {path} returned no content.");
}
