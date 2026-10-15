using ApexCharts;
using Bunit;
using MudBlazor.Services;

namespace Apollo.Web.Client.Tests;

/// <summary>
/// bUnit context with the services the Apollo UI needs. MudBlazor and ApexCharts call into JS,
/// which bUnit cannot run, so JS interop is loose (unplanned calls return defaults).
/// </summary>
public abstract class ApolloTestContext : BunitContext
{
    protected ApolloTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddApexCharts();
    }
}
