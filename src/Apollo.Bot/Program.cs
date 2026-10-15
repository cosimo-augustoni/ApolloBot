using Apollo.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();

var host = builder.Build();

// In Development the AppHost starts the bot only after the migrationservice finished; Swarm has no start order.
if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    await host.Services.WaitForMigrationsAsync();
}

host.Run();
