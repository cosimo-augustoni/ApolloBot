using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>The BFF session: who is logged in, and logging out. Login itself is a full page load to <c>/auth/login</c>.</summary>
public interface IAuthClient
{
    /// <summary>The logged-in user, or <see langword="null"/> when the session is anonymous.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken);

    /// <summary>Ends the session (deletes the session cookie).</summary>
    Task LogoutAsync(CancellationToken cancellationToken);
}
