using YamlDotNet.RepresentationModel;

namespace Apollo.IntegrationTests;

/// <summary>
/// Asserts that the published compose file is Swarm-ready without hand editing (#8).
/// </summary>
public sealed class ComposePublishTests(ComposePublishFixture fixture) : IClassFixture<ComposePublishFixture>
{
    private const string Internal = "apollo_internal";

    [Fact]
    public void Publish_ShouldDefineInternalOverlayNetwork()
    {
        var network = Map(Map(fixture.Compose, "networks"), Internal);

        Scalar(network, "driver").ShouldBe("overlay");
        TryScalar(network, "external").ShouldNotBe("true");
    }

    [Fact]
    public void Publish_ShouldDefineExternalReverseProxyNetworkFromConfiguration()
    {
        var network = Map(Map(fixture.Compose, "networks"), ComposePublishFixture.ReverseProxyNetwork);

        Scalar(network, "driver").ShouldBe("overlay");
        Scalar(network, "external").ShouldBe("true");
    }

    [Fact]
    public void Publish_WebService_ShouldJoinInternalAndReverseProxyNetworksAsSeparateEntries()
    {
        Networks("apollo-web").ShouldBe([Internal, ComposePublishFixture.ReverseProxyNetwork], ignoreOrder: true);
    }

    [Fact]
    public void Publish_NoNetworkEntry_ShouldContainComma()
    {
        foreach (var name in Services.Children.Keys.Cast<YamlScalarNode>().Select(k => k.Value!))
        {
            Networks(name).ShouldAllBe(n => !n.Contains(','), $"service {name}");
        }
    }

    [Theory]
    [InlineData("db")]
    [InlineData("bot")]
    [InlineData("migrationservice")]
    [InlineData("pgadmin")]
    public void Publish_BackendService_ShouldJoinInternalNetworkOnly(string service)
    {
        Networks(service).ShouldBe([Internal]);
    }

    [Fact]
    public void Publish_MigrationService_ShouldBeRestartedBySwarmOnlyOnFailure()
    {
        // Exit 0 is never restarted (runs once); a failed run is retried, e.g. while db still pulls or runs initdb.
        var restartPolicy = Map(Map(Service("migrationservice"), "deploy"), "restart_policy");

        Scalar(restartPolicy, "condition").ShouldBe("on-failure");
        Scalar(restartPolicy, "delay").ShouldBe("10s");
        Scalar(restartPolicy, "max_attempts").ShouldBe("10");
    }

    [Fact]
    public void Publish_Bot_ShouldKeepRestartingOnCrash()
    {
        var bot = Service("bot");

        TryScalar(bot, "restart").ShouldNotBe("no");
        if (bot.Children.TryGetValue(new YamlScalarNode("deploy"), out var deploy)
            && ((YamlMappingNode)deploy).Children.TryGetValue(new YamlScalarNode("restart_policy"), out var policy))
        {
            TryScalar((YamlMappingNode)policy, "condition").ShouldNotBe("none");
        }
    }

    [Fact]
    public void Publish_NoService_ShouldHaveExposeSection()
    {
        foreach (var (key, service) in Services.Children)
        {
            ((YamlMappingNode)service).Children.ContainsKey(new YamlScalarNode("expose"))
                .ShouldBeFalse($"service {key} has expose:");
        }
    }

    [Fact]
    public void Publish_NoService_ShouldHaveDependsOn()
    {
        // Swarm ignores ordering, and its stack schema rejects the long depends_on form Aspire emits.
        foreach (var (key, service) in Services.Children)
        {
            ((YamlMappingNode)service).Children.ContainsKey(new YamlScalarNode("depends_on"))
                .ShouldBeFalse($"service {key} has depends_on:");
        }
    }

    [Fact]
    public void Publish_ShouldNotContainAspireDashboard()
    {
        Services.Children.Keys.Cast<YamlScalarNode>().Select(k => k.Value)
            .ShouldBe(["db", "migrationservice", "bot", "apollo-web", "pgadmin"], ignoreOrder: true);
    }

    [Fact]
    public void Publish_Db_ShouldBindMountDataPathAtPostgres18DataRoot()
    {
        var volume = Volumes("db").ShouldHaveSingleItem();

        Scalar(volume, "type").ShouldBe("bind");
        Scalar(volume, "source").ShouldBe("${POSTGRESDATAPATH}");
        Scalar(volume, "target").ShouldBe("/var/lib/postgresql");
    }

    [Fact]
    public void Publish_PgAdmin_ShouldUsePinnedImage()
    {
        Scalar(Service("pgadmin"), "image").ShouldMatch(@"^dpage/pgadmin4:\d+(\.\d+)+$");
    }

    [Fact]
    public void Publish_PgAdmin_ShouldPublishPortFromParameter()
    {
        var ports = (YamlSequenceNode)Service("pgadmin")["ports"];

        ports.Children.Cast<YamlScalarNode>().Select(p => p.Value).ShouldBe(["${PGADMINPORT}:80"]);
    }

    [Fact]
    public void Publish_PgAdmin_ShouldBindMountDataPath()
    {
        var volume = Volumes("pgadmin").ShouldHaveSingleItem();

        Scalar(volume, "type").ShouldBe("bind");
        Scalar(volume, "source").ShouldBe("${PGADMINDATAPATH}");
        Scalar(volume, "target").ShouldBe("/var/lib/pgadmin");
    }

    [Fact]
    public void Publish_PgAdmin_ShouldBeConfiguredFromParameters()
    {
        var environment = Map(Service("pgadmin"), "environment");

        Scalar(environment, "PGADMIN_DEFAULT_EMAIL").ShouldBe("${PGADMINEMAIL}");
        Scalar(environment, "PGADMIN_DEFAULT_PASSWORD").ShouldBe("${PGADMINPASSWORD}");
        Scalar(environment, "PGADMIN_CONFIG_ENHANCED_COOKIE_PROTECTION").ShouldBe("True");
        Scalar(environment, "PGADMIN_CONFIG_CONSOLE_LOG_LEVEL").ShouldBe("30");
    }

    [Fact]
    public void Publish_Web_ShouldListenOnPort80()
    {
        Scalar(Map(Service("apollo-web"), "environment"), "HTTP_PORTS").ShouldBe("80");
    }

    [Fact]
    public void Publish_Web_ShouldNotPublishHostPorts()
    {
        // Only the reverse proxy reaches apollo-web, over the shared overlay network.
        Service("apollo-web").Children.ContainsKey(new YamlScalarNode("ports")).ShouldBeFalse();
    }

    [Fact]
    public void Publish_EnvFile_ShouldDeclarePgAdminPasswordAsSecretPlaceholder()
    {
        // Proves the password is a parameter that only lands in the git-ignored env file.
        var envFile = Directory.GetFiles(fixture.OutputPath, ".env*").ShouldHaveSingleItem();

        File.ReadAllLines(envFile).ShouldContain(l => l.StartsWith("PGADMINPASSWORD=", StringComparison.Ordinal));
    }

    private YamlMappingNode Services => Map(fixture.Compose, "services");

    private YamlMappingNode Service(string name) => Map(Services, name);

    private IEnumerable<string> Networks(string service) =>
        Service(service).Children.TryGetValue(new YamlScalarNode("networks"), out var networks)
            ? ((YamlSequenceNode)networks).Children.Cast<YamlScalarNode>().Select(n => n.Value!).ToList()
            : [];

    private List<YamlMappingNode> Volumes(string service) =>
        ((YamlSequenceNode)Service(service)["volumes"]).Children.Cast<YamlMappingNode>().ToList();

    private static YamlMappingNode Map(YamlMappingNode node, string key) => (YamlMappingNode)node[key];

    private static string Scalar(YamlMappingNode node, string key) => ((YamlScalarNode)node[key]).Value ?? string.Empty;

    private static string? TryScalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? ((YamlScalarNode)value).Value : null;
}
