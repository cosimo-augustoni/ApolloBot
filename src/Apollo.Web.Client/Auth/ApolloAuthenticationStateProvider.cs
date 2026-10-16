using System.Globalization;
using System.Security.Claims;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Components.Authorization;

namespace Apollo.Web.Client.Auth;

/// <summary>
/// The client's auth state, read once from <c>GET /auth/me</c> (BFF: the session cookie is HttpOnly, the client only
/// sees the claims the host returns). An unreachable host counts as anonymous.
/// </summary>
public sealed class ApolloAuthenticationStateProvider(IAuthClient authClient) : AuthenticationStateProvider
{
    /// <summary>The authentication type of a logged-in principal; any non-empty value makes it authenticated.</summary>
    public const string AuthenticationType = "Apollo";

    private static readonly AuthenticationState _anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private Task<AuthenticationState>? _state;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state ??= LoadAsync();

    /// <summary>Ends the session on the host and switches to anonymous.</summary>
    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await authClient.LogoutAsync(cancellationToken);
        _state = Task.FromResult(_anonymous);
        NotifyAuthenticationStateChanged(_state);
    }

    private async Task<AuthenticationState> LoadAsync()
    {
        try
        {
            var user = await authClient.GetCurrentUserAsync(CancellationToken.None);
            return user is null ? _anonymous : new AuthenticationState(ToPrincipal(user));
        }
        catch (HttpRequestException)
        {
            return _anonymous;
        }
    }

    private static ClaimsPrincipal ToPrincipal(CurrentUserDto user)
    {
        List<Claim> claims =
        [
            new(ApolloClaimTypes.UserId, user.UserId.ToString("D", CultureInfo.InvariantCulture)),
            new(ApolloClaimTypes.Platform, user.Platform),
            new(ApolloClaimTypes.DisplayName, user.DisplayName),
        ];
        if (user.AvatarUrl is not null)
        {
            claims.Add(new Claim(ApolloClaimTypes.AvatarUrl, user.AvatarUrl));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType, ApolloClaimTypes.DisplayName, ClaimTypes.Role));
    }
}
