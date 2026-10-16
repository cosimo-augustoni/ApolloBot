using System.Globalization;
using ApexCharts;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Auth;
using Apollo.Web.Client.Formatting;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

// Numbers and dates are formatted in de-CH and UI text is German, whatever the browser language is.
CultureInfo.DefaultThreadCurrentCulture = UiCulture.Culture;
CultureInfo.DefaultThreadCurrentUICulture = UiCulture.Culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddApexCharts();
builder.Services.AddLocalization();

// Same-origin API: the host serves both the client and /api.
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddApolloApiClients(builder.Configuration);

// BFF: the auth state comes from the host's /auth/me; the session cookie never reaches this code.
builder.Services.AddApolloAuthentication();

await builder.Build().RunAsync();
