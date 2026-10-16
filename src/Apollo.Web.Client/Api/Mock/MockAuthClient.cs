using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IAuthClient"/> without a host: <see cref="User"/> is logged in until <see cref="LogoutAsync"/>, for the
/// prototype and bUnit tests.
/// </summary>
public sealed class MockAuthClient : IAuthClient
{
    public static CurrentUserDto User { get; } = new(Guid.Parse("00000000-0000-0000-0000-00000000a001"), "Discord", "Mock-Nutzer");

    private bool _loggedIn = true;

    public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_loggedIn ? User : null);

    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        _loggedIn = false;
        return Task.CompletedTask;
    }
}
