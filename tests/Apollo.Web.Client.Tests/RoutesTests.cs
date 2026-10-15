using Apollo.Web.Client.Layout;
using Apollo.Web.Client.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Web.Client.Tests;

public sealed class RoutesTests : ApolloTestContext
{
    /// <summary>Every navigation route with its page heading from the wireframes (#27).</summary>
    public static TheoryData<string, string> PlaceholderRoutes => new()
    {
        { "admin", "Bot-Status" },
        { "admin/dashboard", "Server-Übersicht" },
        { "admin/members", "Mitglieder" },
        { "admin/recaps", "Server-Rückblicke" },
        { "admin/data", "Daten" },
        { "admin/settings", "Einstellungen" },
        { "me", "Meine Statistik" },
        { "me/recaps", "Meine Rückblicke" },
        { "me/privacy", "Privatsphäre" },
        { "datenschutz", "Datenschutz" },
    };

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
    public void Router_WhenRouteIsNavigationArea_ShouldShowPlaceholderWithPageHeading(string href, string heading)
    {
        var routes = RenderAt(href);

        routes.WaitForAssertion(() => routes.FindComponents<Placeholder>().ShouldHaveSingleItem());
        routes.Find("h1").TextContent.Trim().ShouldBe(heading);
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
