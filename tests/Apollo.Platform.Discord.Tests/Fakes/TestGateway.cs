using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.JsonModels;
using NetCord.JsonModels;
using NetCord.Rest;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// A <see cref="GatewayClient"/> that is never connected: REST goes to <see cref="Rest"/>, and the cache starts with
/// the given guilds, as if their GUILD_CREATE had arrived.
/// </summary>
internal sealed class TestGateway : IDisposable
{
    public TestGateway(params JsonGuild[] guilds)
    {
        var cache = new JsonGatewayClientCache
        {
            User = new JsonUser { Id = 999, Username = "apollo", IsBot = true },
            Guilds = guilds,
        };

        Client = new GatewayClient(new BotToken(DiscordHost.Token), new GatewayClientConfiguration
        {
            CacheProvider = ImmutableGatewayClientCacheProvider.FromJson(cache),
            RestClientConfiguration = new RestClientConfiguration { RequestHandler = Rest },
        });
    }

    public RecordingRestRequestHandler Rest { get; } = new();

    public GatewayClient Client { get; }

    public void Dispose() => Client.Dispose();
}
