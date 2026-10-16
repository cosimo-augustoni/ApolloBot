using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using ApexCharts;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Auth;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Formatting;
using Apollo.Web.Client.Tests.Api;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;

namespace Apollo.Web.Client.Tests;

/// <summary>
/// bUnit context with the services the Apollo UI needs. MudBlazor and ApexCharts call into JS,
/// which bUnit cannot run, so JS interop is loose (unplanned calls return defaults).
/// API clients are the mocks (so a mock user is logged in), and the viewport is a desktop (<see cref="Breakpoint.Lg"/>) unless a test changes it.
/// </summary>
public abstract class ApolloTestContext : BunitContext
{
    protected ApolloTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddApexCharts();
        Services.AddLocalization();
        Services.AddSingleton<IBrowserViewportService>(Viewport);
        Services.AddApolloApiClients(CreateConfiguration(useMockData: true));
        UseApolloAuthentication();
    }

    /// <summary>The viewport the drawer sees; set <see cref="FakeBrowserViewportService.Breakpoint"/> before rendering.</summary>
    protected FakeBrowserViewportService Viewport { get; } = new(Breakpoint.Lg);

    /// <summary>
    /// Registers the API clients as <c>Program.cs</c> would for the given setting, with an HTTP backend that answers
    /// every request with <paramref name="appInfo"/>, except <c>/auth/me</c>: <paramref name="currentUser"/>, or 401
    /// (anonymous) when it is <see langword="null"/>. The returned handler records the requests.
    /// </summary>
    protected StubHttpMessageHandler UseApiClients(bool useMockData, AppInfoDto appInfo, CurrentUserDto? currentUser = null)
    {
        var handler = currentUser is null
            ? new StubHttpMessageHandler(appInfo).Respond("/auth/me", HttpStatusCode.Unauthorized)
            : new StubHttpMessageHandler(appInfo).Respond("/auth/me", HttpStatusCode.OK, currentUser);
        Services.AddScoped(_ => handler.CreateClient());
        Services.AddApolloApiClients(CreateConfiguration(useMockData));
        return handler;
    }

    // bUnit pre-registers placeholders that fail any AuthorizeView; the real services (TryAdd) would not replace them.
    private void UseApolloAuthentication()
    {
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<AuthenticationStateProvider>();
        Services.AddApolloAuthentication();
    }

    /// <summary>Formats like the browser client does (<c>Program.cs</c>), whatever the machine's culture is.</summary>
    [ModuleInitializer]
    internal static void UseUiCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = UiCulture.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = UiCulture.Culture;
    }

    private static IConfiguration CreateConfiguration(bool useMockData) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ApiClientOptions.SectionName}:{nameof(ApiClientOptions.UseMockData)}"] = useMockData.ToString(),
            })
            .Build();
}
