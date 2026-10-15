using ApexCharts;
using Apollo.Data;
using Apollo.Web.Api;
using Apollo.Web.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApolloDbContext();

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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Apollo.Web.Client._Imports).Assembly);

app.MapAppInfoEndpoints();
app.MapDefaultEndpoints();

app.Run();
