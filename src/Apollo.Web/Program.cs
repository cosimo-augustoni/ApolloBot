using ApexCharts;
using Apollo.Data;
using Apollo.Platform.Discord.Identity;
using Apollo.Web.Api;
using Apollo.Web.Auth;
using Apollo.Web.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();

// BFF login (#29): session cookie with internal claims; the platform OAuth handlers live in the adapter projects.
builder.Services.AddApolloAuthentication();
builder.Services.AddDiscordIdentityProvider();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Prerendering is off (App.razor), but keep the client UI services resolvable on the server.
builder.Services.AddMudServices();
builder.Services.AddApexCharts();

var app = builder.Build();

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
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Apollo.Web.Client._Imports).Assembly);

app.MapAppInfoEndpoints();
app.MapAuthEndpoints();
app.MapDefaultEndpoints();

app.Run();
