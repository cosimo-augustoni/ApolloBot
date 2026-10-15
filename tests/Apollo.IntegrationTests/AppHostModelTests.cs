using System.Text.RegularExpressions;
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

    private static PostgresServerResource GetDb(IDistributedApplicationTestingBuilder builder) =>
        builder.Resources.OfType<PostgresServerResource>().Single(r => r.Name == "db");

    private static Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync(string environment) =>
        DistributedApplicationTestingBuilder.CreateAsync<Projects.Apollo_AppHost>(
            [],
            (_, hostSettings) => hostSettings.EnvironmentName = environment,
            TestContext.Current.CancellationToken);

    // e.g. 2.30.2-pg18: a concrete Timescale version for a concrete Postgres major, never latest.
    [GeneratedRegex(@"^\d+\.\d+\.\d+-pg\d+$")]
    private static partial Regex ConcreteTimescaleTag();
}
