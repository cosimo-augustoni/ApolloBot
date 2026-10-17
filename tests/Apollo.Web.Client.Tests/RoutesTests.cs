using System.Reflection;
using Apollo.Web.Client.Auth;
using Apollo.Web.Client.Components;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Layout;
using Apollo.Web.Client.Pages;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Web.Client.Tests;

public sealed class RoutesTests : ApolloTestContext
{
    private static readonly AppInfoDto _appInfo = new("1.4.2", "Production");
    private static readonly CurrentUserDto _nonAdmin = new(Guid.Parse("0b6f3d4e-1a2b-4c3d-8e9f-a0b1c2d3e4f5"), "Discord", "Nelly", null, IsAdmin: false);

    public static TheoryData<string> AdminRoutes => new(NavigationAreas.Items.Select(i => i.Href).Where(h => h.StartsWith("admin", StringComparison.Ordinal)));

    /// <summary>Every navigation route with its page heading from the wireframes (#27).</summary>
    public static TheoryData<string, string> PlaceholderRoutes => new()
    {
        { "admin", "Bot-Status" },
        { "admin/dashboard", "Server-Übersicht" },
        { "admin/members", "Mitglieder" },
        { "admin/recaps", "Server-Rückblicke" },
        { "admin/data", "Daten" },
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

        routes.WaitForAssertion(() => routes.FindComponents<AreaPlaceholder>().ShouldHaveSingleItem());
        routes.Find("h1").TextContent.Trim().ShouldBe(heading);
        routes.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Noch nicht verfügbar");
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public void AdminRoute_WhenAuthenticatedWithoutAdminClaim_ShouldShowKeinZugriff(string href)
    {
        UseApiClients(useMockData: false, _appInfo, _nonAdmin);

        var routes = RenderAt(href);

        routes.WaitForAssertion(() => routes.FindComponents<NoAccess>().ShouldHaveSingleItem());
        routes.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Kein Zugriff");
        routes.Find(".apollo-empty-explanation").TextContent.Trim().ShouldBe("Du hast keine Berechtigung für diesen Bereich.");
        routes.FindComponents<AreaPlaceholder>().ShouldBeEmpty();
        routes.FindComponents<MainLayout>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AdminRoute_WhenAnonymous_ShouldRedirectToLogin()
    {
        UseApiClients(useMockData: false, _appInfo, currentUser: null);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var routes = RenderAt("admin/members?tab=aktiv");

        routes.WaitForAssertion(() => navigation.History.First().Uri.ShouldBe("/auth/login?returnUrl=%2Fadmin%2Fmembers%3Ftab%3Daktiv"));
        navigation.History.First().Options.ForceLoad.ShouldBeTrue();
        routes.FindComponents<AreaPlaceholder>().ShouldBeEmpty();
        routes.FindComponents<NoAccess>().ShouldBeEmpty();
    }

    [Fact]
    public void AdminRoute_WhenAdmin_ShouldShowThePage()
    {
        UseApiClients(useMockData: false, _appInfo, _nonAdmin with { IsAdmin = true });

        var routes = RenderAt("admin/members");

        routes.WaitForAssertion(() => routes.FindComponents<AreaPlaceholder>().ShouldHaveSingleItem());
        routes.FindComponents<NoAccess>().ShouldBeEmpty();
    }

    [Fact]
    public void Router_WhenRouteIsSettings_ShouldShowTheSettingsPage()
    {
        var routes = RenderAt("admin/settings");

        routes.WaitForAssertion(() => routes.FindComponents<Apollo.Web.Client.Pages.Admin.Settings>().ShouldHaveSingleItem());
        routes.Find("h1").TextContent.Trim().ShouldBe("Einstellungen");
        routes.FindComponents<AreaPlaceholder>().ShouldBeEmpty();
    }

    [Fact]
    public void MemberRoute_WhenAuthenticatedWithoutAdminClaim_ShouldStayOpenUntilTheMemberPolicy()
    {
        UseApiClients(useMockData: false, _appInfo, _nonAdmin);

        var routes = RenderAt("me");

        routes.WaitForAssertion(() => routes.FindComponents<AreaPlaceholder>().ShouldHaveSingleItem());
    }

    [Fact]
    public void AdminPages_ShouldRequireTheAdminPolicy()
    {
        var adminPages = typeof(Routes).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any(r => r.Template.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        adminPages.ShouldNotBeEmpty();
        adminPages.Where(t => t.GetCustomAttributes<AuthorizeAttribute>().All(a => a.Policy != ApolloPolicies.Admin)).ShouldBeEmpty();
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
