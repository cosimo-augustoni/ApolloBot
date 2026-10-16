using Apollo.Bot.Commands;
using Apollo.Bot.GuildState;
using Apollo.Data;
using Apollo.Platform.Discord;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();
builder.Services.AddDiscordPlatform();
builder.Services.AddCommandRouting();
builder.Services.AddGuildStateSync();

var host = builder.Build();

// In Development the AppHost starts the bot only after the migrationservice finished; Swarm has no start order.
if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    await host.Services.WaitForMigrationsAsync();
}

host.Run();
