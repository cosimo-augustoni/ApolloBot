using Aspire.Hosting.Testing;
using YamlDotNet.RepresentationModel;

namespace Apollo.IntegrationTests;

/// <summary>
/// Runs the AppHost once in publish mode into a temp directory and parses the generated <c>docker-compose.yaml</c>.
/// Only placeholder values are used; no image is built or pushed.
/// </summary>
public sealed class ComposePublishFixture : IAsyncLifetime
{
    public const string ReverseProxyNetwork = "test-proxy";

    private static readonly TimeSpan _publishTimeout = TimeSpan.FromMinutes(3);

    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), "apollo-publish-" + Guid.NewGuid().ToString("N"));

    private YamlMappingNode? _compose;

    public YamlMappingNode Compose => _compose ?? throw new InvalidOperationException("The compose file has not been published.");

    public string OutputPath => _outputPath;

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(_publishTimeout);

        string[] args =
        [
            "--operation", "publish",
            "--step", "publish",
            "--output-path", _outputPath,
            "--environment", "Production",
            "REGISTRY_ENDPOINT=registry.example.test",
            "REGISTRY_REPOSITORY=apollo",
            $"Apollo:Deploy:ReverseProxyNetwork={ReverseProxyNetwork}",
            "Parameters:PostgresDataPath=/srv/apollo/db",
            "Parameters:PgAdminDataPath=/srv/apollo/pgadmin",
            "Parameters:PgAdminPort=5050",
            "Parameters:PgAdminEmail=admin@example.test",
            "Parameters:PgAdminPassword=placeholder-password",
            "Parameters:db-password=placeholder-password",
            "Parameters:PublicWebUrl=https://apollo.example.test",
            "Parameters:DiscordOAuthClientId=123456789012345678",
            "Parameters:DiscordOAuthClientSecret=placeholder-secret",
        ];

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Apollo_AppHost>(args, cts.Token);
        await using var app = await builder.BuildAsync(cts.Token);
        await app.RunAsync(cts.Token);

        var composePath = Path.Combine(_outputPath, "docker-compose.yaml");
        using var reader = new StreamReader(composePath);
        var stream = new YamlStream();
        stream.Load(reader);
        _compose = (YamlMappingNode)stream.Documents[0].RootNode;
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
