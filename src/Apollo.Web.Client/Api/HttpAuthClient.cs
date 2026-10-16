using System.Net;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>
/// <see cref="IAuthClient"/> against the same-origin host. The session cookie is HttpOnly, so the browser sends it
/// and this client never sees it.
/// </summary>
public sealed class HttpAuthClient(HttpClient http) : IAuthClient
{
    public const string MePath = "auth/me";
    public const string AntiforgeryPath = "auth/antiforgery";
    public const string LogoutPath = "auth/logout";

    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(MePath, UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentUserDto>(cancellationToken)
            ?? throw new InvalidOperationException($"GET {MePath} returned no content.");
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        var antiforgery = await http.GetFromJsonAsync<AntiforgeryTokenDto>(new Uri(AntiforgeryPath, UriKind.Relative), cancellationToken)
            ?? throw new InvalidOperationException($"GET {AntiforgeryPath} returned no content.");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(LogoutPath, UriKind.Relative));
        request.Headers.Add(antiforgery.HeaderName, antiforgery.RequestToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
