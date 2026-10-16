using NetCord.Gateway;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// Records the gateway commands the adapter sends and lets a test answer them, e.g. with member chunks.
/// </summary>
internal sealed class FakeGatewayCommands : IDiscordGatewayCommands
{
    private readonly List<GuildUsersRequestProperties> _guildUserRequests = [];

    public IReadOnlyList<GuildUsersRequestProperties> GuildUserRequests => _guildUserRequests;

    /// <summary>
    /// Runs for every member request, after it was recorded.
    /// </summary>
    public Func<GuildUsersRequestProperties, ValueTask> OnGuildUserRequest { get; set; } = _ => default;

    public async ValueTask RequestGuildUsersAsync(GuildUsersRequestProperties request, CancellationToken cancellationToken)
    {
        _guildUserRequests.Add(request);
        await OnGuildUserRequest(request);
    }
}
