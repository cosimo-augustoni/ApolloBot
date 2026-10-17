using Apollo.Bot.Commands;
using Apollo.Bot.GuildState;
using Apollo.Bot.Membership;
using Apollo.Bot.Messages;
using Apollo.Bot.Presence;
using Apollo.Bot.Reactions;
using Apollo.Bot.Stats;
using Apollo.Bot.Status;
using Apollo.Bot.Tracking;
using Apollo.Bot.Voice;
using Apollo.Data;
using Apollo.Platform.Discord;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();
builder.Services.AddDiscordPlatform();
builder.Services.AddCommandRouting();
builder.Services.AddGuildStateSync();
builder.Services.AddTrackingGate();
builder.Services.AddTrackingCommands();
builder.Services.AddStatsCommands();
builder.Services.AddMessageRecording();
builder.Services.AddVoiceRecording();
builder.Services.AddPresenceRecording();
builder.Services.AddReactionRecording();
builder.Services.AddMembershipRecording();
builder.Services.AddBotStatus();

var host = builder.Build();

// In Development the AppHost starts the bot only after the migrationservice finished; Swarm has no start order.
if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    await host.Services.WaitForMigrationsAsync();
}

host.Run();
