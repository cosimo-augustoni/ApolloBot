using Apollo.AppHost;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var db = AddPostgres(builder);
var apollo = db.AddDatabase("apollo");

builder.AddProject<Apollo_Bot>("bot")
    .WithReference(apollo)
    .WaitFor(db);

builder.AddProject<Apollo_Web>("web")
    .WithReference(apollo)
    .WaitFor(db)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();

static IResourceBuilder<PostgresServerResource> AddPostgres(IDistributedApplicationBuilder builder)
{
    var db = builder.AddPostgres("db")
        .WithImage(TimescaleImage.Image, TimescaleImage.Tag)
        .WithLifetime(ContainerLifetime.Persistent);

    // Production storage (bind mount, compose service) is added by #8.
    if (builder.Environment.IsDevelopment())
    {
        // Not WithDataVolume(): it mounts at /var/lib/postgresql/data, which Postgres 18+ images refuse.
        db.WithVolume(VolumeNameGenerator.Generate(db, "data"), TimescaleImage.DataRoot)
            .WithPgAdmin();
    }

    return db;
}
