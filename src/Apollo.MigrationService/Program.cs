using Apollo.Data;
using Apollo.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();

builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(MigrationWorker.ActivitySourceName));
builder.Services.AddSingleton<DatabaseMigrator>();
builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
