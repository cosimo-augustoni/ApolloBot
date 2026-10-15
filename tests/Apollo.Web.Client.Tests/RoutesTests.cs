using Apollo.Web.Client.Layout;
using Apollo.Web.Client.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Web.Client.Tests;

public sealed class RoutesTests : ApolloTestContext
{
    public static TheoryData<string> PlaceholderRoutes => [.. NavigationAreas.Items.Select(i => i.Href).Where(h => h.Length > 0)];

    [Fact]
    public void Router_WhenRouteUnknown_ShouldShowNotFoundPage()
    {
        var routes = RenderAt("gibt-es-nicht");

        routes.WaitForAssertion(() => routes.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Seite nicht gefunden"));
        routes.FindComponents<MainLayout>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Router_WhenRouteIsRoot_ShouldShowHomePage()
    {
        var routes = RenderAt("");

        routes.WaitForAssertion(() => routes.FindComponents<Home>().ShouldHaveSingleItem());
        routes.Find("h1").TextContent.Trim().ShouldBe("Willkommen bei Apollo");
    }

    [Theory]
    [MemberData(nameof(PlaceholderRoutes))]
    public void Router_WhenRouteIsNavigationArea_ShouldShowPlaceholderWithAreaName(string href)
    {
        var routes = RenderAt(href);

        routes.WaitForAssertion(() => routes.FindComponents<Placeholder>().ShouldHaveSingleItem());
        routes.Find("h1").TextContent.Trim().ShouldNotBeNullOrWhiteSpace();
        routes.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Noch nicht verfügbar");
    }

    [Fact]
    public void Router_WhenRouteIsStyleGuide_ShouldShowStyleGuide()
    {
        var routes = RenderAt("styleguide");

        routes.WaitForAssertion(() => routes.FindComponents<StyleGuide>().ShouldHaveSingleItem());
    }

    private IRenderedComponent<Routes> RenderAt(string relativePath)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(relativePath);
        return Render<Routes>();
    }
}
