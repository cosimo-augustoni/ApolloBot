using Apollo.AppHost;
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes.Swarm;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var registry = AddRegistry(builder);

var db = AddPostgres(builder);
var apollo = db.AddDatabase("apollo");

// Applies EF migrations, then exits (#6). Swarm restarts it only after a non-zero exit, e.g. when db was still
// pulling its image or running initdb for longer than the migrator's own retries; a successful run stays done.
var migrations = builder.AddProject<Apollo_MigrationService>("migrationservice")
    .WithReference(apollo)
    .WaitFor(db)
    .WithContainerRegistry(registry)
    .PublishAsDockerComposeService((_, service) =>
    {
        UseSwarm(service, ComposeNetworks.Internal);
        service.Deploy = new Deploy
        {
            RestartPolicy = new RestartPolicy { Condition = "on-failure", Delay = "10s", MaxAttempts = 10 },
        };
    });

// WaitForCompletion orders the run mode only; the published stack has no depends_on (UseSwarm), so outside
// Development bot and web wait for the migrations themselves (WaitForMigrationsAsync).
var bot = builder.AddProject<Apollo_Bot>("bot")
    .WithReference(apollo)
    .WaitForCompletion(migrations)
    .WithContainerRegistry(registry)
    .PublishAsDockerComposeService((_, service) => UseSwarm(service, ComposeNetworks.Internal));

var reverseProxyNetwork = builder.Configuration["Apollo:Deploy:ReverseProxyNetwork"]
    ?? throw new InvalidOperationException("Apollo:Deploy:ReverseProxyNetwork is not configured.");

// The service name is its DNS alias on the shared reverse-proxy network, so it must be unique there.
var web = builder.AddProject<Apollo_Web>("apollo-web")
    .WithReference(apollo)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithContainerRegistry(registry)
    .PublishAsDockerComposeService((_, service) =>
    {
        UseSwarm(service, ComposeNetworks.Internal, reverseProxyNetwork);
        service.Ports = [];
    });

if (builder.ExecutionContext.IsPublishMode)
{
    // The reverse proxy targets apollo-web:80.
    web.WithEndpoint("http", endpoint => endpoint.TargetPort = 80);

    AddPgAdmin(builder);
}

// The bot links website pages in its replies (#20). Aspire only knows the internal address of apollo-web in the
// stack (http://apollo-web:80); its public domain lives in the reverse proxy, so production passes it as a parameter.
if (builder.ExecutionContext.IsPublishMode)
{
    bot.WithEnvironment("Apollo__Links__PublicWebUrl", builder.AddParameter("PublicWebUrl"));
}
else
{
    bot.WithEnvironment("Apollo__Links__PublicWebUrl", PreferredEndpoint(web));
}

builder.AddDockerComposeEnvironment("docker-compose")
    .WithDashboard(false)
    .ConfigureComposeFile(file => file.Networks = new Dictionary<string, Network>
    {
        [ComposeNetworks.Internal] = new() { Name = ComposeNetworks.Internal, Driver = "overlay", External = false },
        [reverseProxyNetwork] = new() { Name = reverseProxyNetwork, Driver = "overlay", External = true },
    });

// `aspire do prepare-stack`: builds and pushes the images and writes .env.<environment> with their tags, in one run so
// the tags match. Not `aspire deploy`: that would also run `docker compose up` on this machine (docs/deployment.md).
// The compose steps only exist in publish mode.
if (builder.ExecutionContext.IsPublishMode)
{
    builder.Pipeline.AddStep("prepare-stack", _ => Task.CompletedTask, dependsOn: new[] { "push", "prepare-docker-compose" });
}

builder.Build().Run();

static IResourceBuilder<ContainerRegistryResource> AddRegistry(IDistributedApplicationBuilder builder)
{
    var endpoint = builder.AddParameterFromConfiguration("registryEndpoint", "REGISTRY_ENDPOINT");
    var repository = builder.AddParameterFromConfiguration("registryRepository", "REGISTRY_REPOSITORY");

    return builder.AddContainerRegistry("kallisto", endpoint, repository);
}

static IResourceBuilder<PostgresServerResource> AddPostgres(IDistributedApplicationBuilder builder)
{
    var db = builder.AddPostgres("db")
        .WithImage(TimescaleImage.Image, TimescaleImage.Tag)
        .WithLifetime(ContainerLifetime.Persistent);

    if (builder.ExecutionContext.IsPublishMode)
    {
        var dataPath = builder.AddParameter("PostgresDataPath");

        // Not WithBindMount(): it resolves the source relative to the AppHost directory, but here it is a host path
        // on the Swarm node. Mounted at the Postgres 18+ data root, like the dev volume below.
        db.PublishAsDockerComposeService((resource, service) =>
        {
            UseSwarm(service, ComposeNetworks.Internal);
            service.Volumes = [BindMount(dataPath.AsEnvironmentPlaceholder(resource), TimescaleImage.DataRoot)];
        });
    }
    else if (builder.Environment.IsDevelopment())
    {
        // Not WithDataVolume(): it mounts at /var/lib/postgresql/data, which Postgres 18+ images refuse.
        db.WithVolume(VolumeNameGenerator.Generate(db, "data"), TimescaleImage.DataRoot)
            .WithPgAdmin();
    }

    return db;
}

// WithPgAdmin() only applies in run mode, but pgAdmin is wanted in production too.
static void AddPgAdmin(IDistributedApplicationBuilder builder)
{
    var email = builder.AddParameter("PgAdminEmail");
    var password = builder.AddParameter("PgAdminPassword", secret: true);
    var dataPath = builder.AddParameter("PgAdminDataPath");
    var port = builder.AddParameter("PgAdminPort");

    builder.AddContainer("pgadmin", PgAdminImage.Image, PgAdminImage.Tag)
        .WithEnvironment("PGADMIN_DEFAULT_EMAIL", email)
        .WithEnvironment("PGADMIN_DEFAULT_PASSWORD", password)
        .WithEnvironment("PGADMIN_CONFIG_ENHANCED_COOKIE_PROTECTION", "True")
        .WithEnvironment("PGADMIN_CONFIG_CONSOLE_LOG_LEVEL", "30")
        .WithHttpEndpoint(targetPort: 80)
        .PublishAsDockerComposeService((resource, service) =>
        {
            UseSwarm(service, ComposeNetworks.Internal);
            service.Ports = [$"{port.AsEnvironmentPlaceholder(resource)}:80"];
            service.Volumes = [BindMount(dataPath.AsEnvironmentPlaceholder(resource), "/var/lib/pgadmin")];
        });
}

// Settings every service needs to deploy as a Swarm stack without hand editing (docs/deployment.md):
// separate network entries, no expose: (ignored by Swarm) and no depends_on (ignored by Swarm, and the stack schema
// rejects the long form Aspire emits; bot and web wait for migrations themselves, #6).
static void UseSwarm(Service service, params string[] networks)
{
    service.Networks = [.. networks];
    service.Expose = [];
    service.DependsOn = [];
}

// The web host's endpoints come from its launch profile: https when that profile has one, else http.
static EndpointReference PreferredEndpoint(IResourceBuilder<ProjectResource> project) =>
    project.GetEndpoint(project.Resource.Annotations.OfType<EndpointAnnotation>().Any(e => e.Name == "https") ? "https" : "http");

static Volume BindMount(string source, string target) => new() { Name = source, Type = "bind", Source = source, Target = target };
