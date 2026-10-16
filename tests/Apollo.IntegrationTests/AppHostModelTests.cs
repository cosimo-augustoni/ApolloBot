using System.Text.RegularExpressions;
using Apollo.Testing.Data;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Postgres;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests;

/// <summary>
/// Inspects the AppHost's resource model without starting anything (no Docker needed).
/// </summary>
public sealed partial class AppHostModelTests
{
    [Fact]
    public async Task AppHost_WhenDevelopment_ShouldAddPgAdmin()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);

        builder.Resources.OfType<PgAdminContainerResource>().ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task AppHost_WhenNotDevelopment_ShouldNotAddPgAdmin(string environment)
    {
        await using var builder = await CreateBuilderAsync(environment);

        builder.Resources.OfType<PgAdminContainerResource>().ShouldBeEmpty();
    }

    [Fact]
    public async Task AppHost_WhenDevelopment_ShouldNotAddPublishOnlyPgAdminContainer()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);

        builder.Resources.Where(r => r.Name == "pgadmin").ShouldHaveSingleItem().ShouldBeOfType<PgAdminContainerResource>();
    }

    [Theory]
    [InlineData("bot")]
    [InlineData("apollo-web")]
    [InlineData("migrationservice")]
    public async Task AppHost_Project_ShouldPushToKallistoRegistry(string resourceName)
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);
        var project = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == resourceName);

#pragma warning disable ASPIRECOMPUTE003 // Registry APIs are experimental in Aspire 13.6.
        project.Annotations.OfType<ContainerRegistryReferenceAnnotation>().ShouldHaveSingleItem()
            .Registry.ShouldBeOfType<ContainerRegistryResource>().Name.ShouldBe("kallisto");
#pragma warning restore ASPIRECOMPUTE003
    }

    [Fact]
    public async Task AppHost_Db_ShouldUsePinnedTimescaleImage()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);

        var image = GetDb(builder).Annotations.OfType<ContainerImageAnnotation>().Single();

        image.Image.ShouldBe("timescale/timescaledb");
        image.Tag.ShouldNotBeNull().ShouldMatch(ConcreteTimescaleTag().ToString());
    }

    [Fact]
    public async Task TimescaleImage_ShouldBeSharedWithAppHost()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);

        var image = GetDb(builder).Annotations.OfType<ContainerImageAnnotation>().Single();

        TimescaleFixture.Image.ShouldBe($"{image.Image}:{image.Tag}");
    }

    [Fact]
    public async Task AppHost_Db_ShouldUsePersistentLifetime()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);

        GetDb(builder).Annotations.OfType<ContainerLifetimeAnnotation>().Single().Lifetime
            .ShouldBe(ContainerLifetime.Persistent);
    }

    [Fact]
    public async Task AppHost_WhenDevelopment_ShouldMountNamedDataVolumeAtPostgres18DataRoot()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);

        // Postgres 18+ images refuse to start with a mount at /var/lib/postgresql/data (Aspire's WithDataVolume default).
        var volume = GetDb(builder).Annotations.OfType<ContainerMountAnnotation>().ShouldHaveSingleItem();

        volume.Type.ShouldBe(ContainerMountType.Volume);
        volume.Source.ShouldNotBeNullOrWhiteSpace();
        volume.Target.ShouldBe("/var/lib/postgresql");
    }

    [Theory]
    [InlineData("bot")]
    [InlineData("apollo-web")]
    public async Task AppHost_Project_ShouldWaitForMigrationServiceCompletion(string resourceName)
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);
        var project = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == resourceName);

        project.Annotations.OfType<WaitAnnotation>()
            .ShouldContain(w => w.Resource.Name == "migrationservice" && w.WaitType == WaitType.WaitForCompletion);
    }

    [Fact]
    public async Task AppHost_MigrationService_ShouldWaitForDb()
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);
        var project = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == "migrationservice");

        project.Annotations.OfType<WaitAnnotation>().ShouldContain(w => w.Resource.Name == "db");
    }

    [Theory]
    [InlineData("bot")]
    [InlineData("apollo-web")]
    [InlineData("migrationservice")]
    public async Task AppHost_Project_ShouldReceiveApolloConnectionString(string resourceName)
    {
        await using var builder = await CreateBuilderAsync(Environments.Development);
        var project = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == resourceName);

        var configuration = await ExecutionConfigurationBuilder.Create(project)
            .WithEnvironmentVariablesConfig()
            .BuildAsync(
                new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
                NullLogger.Instance,
                TestContext.Current.CancellationToken);

        configuration.EnvironmentVariables.Select(e => e.Key).ShouldContain("ConnectionStrings__apollo");
    }

    [Theory]
    [InlineData("https", "https://localhost:7174")]
    [InlineData("http", "http://localhost:5008")]
    public async Task AppHost_WhenRunning_ShouldPassTheWebUrlOfTheLaunchProfileToTheBot(string launchProfile, string expectedUrl)
    {
        // The web host's endpoints come from src/Apollo.Web/Properties/launchSettings.json, picked by the AppHost profile.
        await using var builder = await CreateBuilderAsync(Environments.Development, $"DOTNET_LAUNCH_PROFILE={launchProfile}");
        var bot = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == "bot");
        var web = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == "apollo-web");
        foreach (var annotation in web.Annotations.OfType<EndpointAnnotation>())
        {
            // Normally DCP allocates them when the app runs; here they get their launch-profile port.
            annotation.AllocatedEndpoint = new AllocatedEndpoint(annotation, "localhost", annotation.Port!.Value);
        }

        var environment = await GetEnvironmentAsync(bot, DistributedApplicationOperation.Run);

        var endpoint = environment["Apollo__Links__PublicWebUrl"].ShouldBeOfType<EndpointReference>();
        endpoint.Resource.ShouldBe(web);
        endpoint.Url.ShouldBe(expectedUrl);
    }

    [Fact]
    public async Task AppHost_WhenPublishing_ShouldPassThePublicWebUrlParameterToTheBot()
    {
        await using var builder = await CreateBuilderAsync(Environments.Production, "--operation", "publish");
        var bot = builder.Resources.OfType<ProjectResource>().Single(r => r.Name == "bot");

        var environment = await GetEnvironmentAsync(bot, DistributedApplicationOperation.Publish);

        environment["Apollo__Links__PublicWebUrl"].ShouldBeOfType<ParameterResource>().Name.ShouldBe("PublicWebUrl");
    }

    private static async Task<Dictionary<string, object>> GetEnvironmentAsync(IResource resource, DistributedApplicationOperation operation)
    {
        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(operation),
            cancellationToken: TestContext.Current.CancellationToken);
        foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(context);
        }

        return context.EnvironmentVariables;
    }

    private static PostgresServerResource GetDb(IDistributedApplicationTestingBuilder builder) =>
        builder.Resources.OfType<PostgresServerResource>().Single(r => r.Name == "db");

    private static Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(string environment, params string[] args) =>
        DistributedApplicationTestingBuilder.CreateAsync<Projects.Apollo_AppHost>(
            args,
            (_, hostSettings) => hostSettings.EnvironmentName = environment,
            TestContext.Current.CancellationToken);

    // e.g. 2.30.2-pg18: a concrete Timescale version for a concrete Postgres major, never latest.
    [GeneratedRegex(@"^\d+\.\d+\.\d+-pg\d+$")]
    private static partial Regex ConcreteTimescaleTag();
}
