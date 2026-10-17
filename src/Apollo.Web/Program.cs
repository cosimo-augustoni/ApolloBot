using ApexCharts;
using Apollo.Data;
using Apollo.Data.Tracking;
using Apollo.Platform.Discord.Identity;
using Apollo.Web.Admin;
using Apollo.Web.Api;
using Apollo.Web.Auth;
using Apollo.Web.Components;
using Microsoft.AspNetCore.Diagnostics;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();

// BFF login (#29): session cookie with internal claims; the platform OAuth handlers live in the adapter projects.
builder.Services.AddApolloAuthentication();
builder.Services.AddDiscordIdentityProvider();

// Admin settings page (#33): role settings of the configured guild per platform.
builder.Services.AddScoped<RoleSettingsQuery>();
builder.Services.AddScoped<RoleSettingsStore>();

// Admin bot status page (#31): latest heartbeat and events per hour.
builder.Services.AddOptions<BotStatusOptions>()
    .BindConfiguration(BotStatusOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<BotStatusQuery>();

// Admin data page (#32): event counts, tracked members and the raw event browser.
builder.Services.AddScoped<DataOverviewQuery>();
builder.Services.AddScoped<RawEventQuery>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Prerendering is off (App.razor), but keep the client UI services resolvable on the server.
builder.Services.AddMudServices();
builder.Services.AddApexCharts();

var app = builder.Build();

AdminAccessStartupLog.Write(app.Services);

// In Development the AppHost starts web only after the migrationservice finished; Swarm has no start order.
if (!app.Environment.IsDevelopment())
{
    await app.Services.WaitForMigrationsAsync();
}

// First, so the scheme and host of the public URL apply to everything after it (OAuth redirect URI, secure cookies).
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// The not-found page is for browsers; API callers (the WASM client) get bare status codes.
app.Use((context, next) =>
{
    if (AuthServiceCollectionExtensions.IsApiRequest(context.Request)
        && context.Features.Get<IStatusCodePagesFeature>() is { } statusCodePages)
    {
        statusCodePages.Enabled = false;
    }

    return next(context);
});
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Apollo.Web.Client._Imports).Assembly);

app.MapAppInfoEndpoints();
app.MapBotStatusEndpoints();
app.MapDataEndpoints();
app.MapRoleSettingsEndpoints();
app.MapAuthEndpoints();
app.MapDefaultEndpoints();

app.Run();
