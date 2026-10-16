namespace Apollo.Web.Client.Contracts;

/// <summary>
/// Response of <c>GET /auth/antiforgery</c>: the request token to send in <paramref name="HeaderName"/> with
/// state-changing requests such as <c>POST /auth/logout</c>. Not an auth token: it is useless without the cookies.
/// </summary>
public sealed record AntiforgeryTokenDto(string HeaderName, string RequestToken);
