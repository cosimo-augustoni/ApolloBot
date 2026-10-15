using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Formatting;
using Apollo.Web.Client.Layout;
using Apollo.Web.Client.Tests.Api;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Layout;

public sealed class MainLayoutTests : ApolloTestContext
{
    [Fact]
    public void MainLayout_ShouldRenderAppBarAndNavigationWithGermanLabels()
    {
        var layout = RenderLayout();

        layout.Find(".mud-appbar").TextContent.ShouldContain("Apollo");
        layout.Find(".apollo-menu-button").GetAttribute("aria-label").ShouldBe("Menü ein- oder ausblenden");
        layout.FindAll(".mud-nav-link").Select(l => l.TextContent.Trim()).ShouldBe(
        [
            "Bot-Status", "Übersicht", "Mitglieder", "Rückblicke", "Daten", "Einstellungen",
            "Übersicht", "Rückblicke", "Privatsphäre",
            "Datenschutz",
        ]);
        layout.FindAll(".apollo-nav-section").Select(s => s.TextContent.Trim()).ShouldBe(["Server", "Meine Statistik", "Info"]);
        layout.Find(".apollo-brand").GetAttribute("href").ShouldBe("/");
        layout.Find(".apollo-body").TextContent.Trim().ShouldBe("Inhalt");
    }

    [Fact]
    public void MainLayout_WhenOnDesktop_ShouldShowOpenPersistentDrawer()
    {
        var layout = RenderLayout();

        IsDrawerOpen(layout).ShouldBeTrue();
        IsOverlayVisible(layout).ShouldBeFalse();
    }

    [Fact]
    public void MainLayout_WhenBelowMdBreakpoint_ShouldStartWithDrawerClosed()
    {
        Viewport.Breakpoint = Breakpoint.Sm;

        var layout = RenderLayout();

        IsDrawerOpen(layout).ShouldBeFalse();
    }

    [Fact]
    public void MainLayout_WhenMenuButtonClicked_ShouldToggleDrawer()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var layout = RenderLayout();

        layout.Find(".apollo-menu-button").Click();

        IsDrawerOpen(layout).ShouldBeTrue();
        IsOverlayVisible(layout).ShouldBeTrue();

        layout.Find(".apollo-menu-button").Click();

        IsDrawerOpen(layout).ShouldBeFalse();
    }

    [Fact]
    public void MainLayout_WhenMenuButtonClickedOnDesktop_ShouldCollapseDrawer()
    {
        var layout = RenderLayout();

        layout.Find(".apollo-menu-button").Click();

        IsDrawerOpen(layout).ShouldBeFalse();
    }

    [Fact]
    public void NavMenu_WhenItemClickedOnMobile_ShouldCloseDrawer()
    {
        Viewport.Breakpoint = Breakpoint.Xs;
        var layout = RenderLayout();
        layout.Find(".apollo-menu-button").Click();

        layout.FindAll(".mud-nav-link")[1].Click();

        IsDrawerOpen(layout).ShouldBeFalse();
    }

    [Fact]
    public void NavMenu_WhenItemClickedOnDesktop_ShouldKeepDrawerOpen()
    {
        var layout = RenderLayout();

        layout.FindAll(".mud-nav-link")[1].Click();

        IsDrawerOpen(layout).ShouldBeTrue();
    }

    [Fact]
    public void MainLayout_WhenUsingMockClient_ShouldShowMockVersion()
    {
        var api = UseApiClients(useMockData: true, new AppInfoDto("9.9.9", "Production"));

        var layout = RenderLayout();

        layout.WaitForAssertion(() =>
            layout.Find(".apollo-app-version").TextContent.Trim().ShouldBe($"Version {MockAppInfoClient.AppInfo.Version}"));
        api.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void MainLayout_WhenUsingHttpClient_ShouldShowVersionFromApi()
    {
        var api = UseApiClients(useMockData: false, new AppInfoDto("1.4.2", "Production"));

        var layout = RenderLayout();

        layout.WaitForAssertion(() => layout.Find(".apollo-app-version").TextContent.Trim().ShouldBe("Version 1.4.2"));
        api.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/app-info");
    }

    [Fact]
    public void MainLayout_WhenApiUnreachable_ShouldShowVersionPlaceholder()
    {
        UseApiClients(useMockData: false, new AppInfoDto("1.4.2", "Production"));
        Services.AddScoped(_ => new HttpClient(new FailingHttpMessageHandler()) { BaseAddress = new Uri("http://apollo.test/") });

        var layout = RenderLayout();

        layout.WaitForAssertion(() => layout.Find(".apollo-app-version").TextContent.Trim().ShouldBe($"Version {UiCulture.Placeholder}"));
    }

    private IRenderedComponent<MainLayout> RenderLayout() =>
        Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddContent(0, "Inhalt"))));

    private static bool IsDrawerOpen(IRenderedComponent<MainLayout> layout)
    {
        var drawer = layout.Find(".mud-drawer");
        drawer.ClassList.ShouldContain("mud-drawer-responsive");
        return drawer.ClassList.Contains("mud-drawer--open");
    }

    private static bool IsOverlayVisible(IRenderedComponent<MainLayout> layout) =>
        layout.FindAll(".mud-overlay").Count > 0;
}
