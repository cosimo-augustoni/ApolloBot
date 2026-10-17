using System.Net;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>
/// <see cref="IRoleSettingsClient"/> against the same-origin API (<c>/api/admin/settings</c>). Writes fetch an
/// antiforgery token first and send it as a header, as <see cref="HttpAuthClient.LogoutAsync"/> does.
/// </summary>
public sealed class HttpRoleSettingsClient(HttpClient http) : IRoleSettingsClient
{
    public const string BasePath = "api/admin/settings";

    public async Task<IReadOnlyList<string>> GetPlatformsAsync(CancellationToken cancellationToken) =>
        await GetAsync<string[]>($"{BasePath}/platforms", cancellationToken);

    public async Task<RoleSettingsDto> GetSettingsAsync(string platform, CancellationToken cancellationToken) =>
        await GetAsync<RoleSettingsDto>(PlatformPath(platform), cancellationToken);

    public async Task<RolePreviewDto> GetPreviewAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        var query = string.Join('&', roleIds.Select(id => $"roleIds={id}"));
        return await GetAsync<RolePreviewDto>($"{PlatformPath(platform)}/preview?{query}", cancellationToken);
    }

    public async Task<IReadOnlyList<RoleSettingChangeDto>> GetHistoryAsync(string platform, int take, CancellationToken cancellationToken) =>
        await GetAsync<RoleSettingChangeDto[]>($"{PlatformPath(platform)}/history?take={take}", cancellationToken);

    public Task SetTrackingRoleAsync(string platform, Guid? roleId, CancellationToken cancellationToken) =>
        PutAsync($"{PlatformPath(platform)}/tracking-role", new TrackingRoleRequest(roleId), cancellationToken);

    public Task SetMemberAccessRolesAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        PutAsync($"{PlatformPath(platform)}/member-access-roles", new MemberAccessRolesRequest([.. roleIds]), cancellationToken);

    private static string PlatformPath(string platform) => $"{BasePath}/{Uri.EscapeDataString(platform)}";

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync<T>(new Uri(path, UriKind.Relative), cancellationToken)
            ?? throw new InvalidOperationException($"GET {path} returned no content.");

    private async Task PutAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        var antiforgery = await GetAsync<AntiforgeryTokenDto>(HttpAuthClient.AntiforgeryPath, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(path, UriKind.Relative)) { Content = JsonContent.Create(body) };
        request.Headers.Add(antiforgery.HeaderName, antiforgery.RequestToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new RoleSettingsRejectedException($"PUT {path} was rejected.");
        }

        response.EnsureSuccessStatusCode();
    }
}
