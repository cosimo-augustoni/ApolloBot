using System.Net;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Formatting;
using Apollo.Web.Client.Layout;
using Apollo.Web.Client.Tests.Api;
using Bunit;
using Bunit.TestDoubles;
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
            "Meine Statistik",
            "Datenschutz",
        ]);
        layout.FindAll(".apollo-nav-section").Select(s => s.TextContent.Trim()).ShouldBe(["Server", "Mitgliederbereich", "Info"]);
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
        api.Requests.Where(r => r.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal)).ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/app-info");
    }

    [Fact]
    public void MainLayout_WhenApiUnreachable_ShouldShowVersionPlaceholder()
    {
        UseApiClients(useMockData: false, new AppInfoDto("1.4.2", "Production"));
        Services.AddScoped(_ => new HttpClient(new FailingHttpMessageHandler()) { BaseAddress = new Uri("http://apollo.test/") });

        var layout = RenderLayout();

        layout.WaitForAssertion(() => layout.Find(".apollo-app-version").TextContent.Trim().ShouldBe($"Version {UiCulture.Placeholder}"));
        layout.WaitForAssertion(() => layout.Find(".apollo-login").TextContent.Trim().ShouldBe("Anmelden"));
    }

    [Fact]
    public void AppBar_WhenAnonymous_ShouldShowAnmelden()
    {
        UseApiClients(useMockData: false, _appInfo);

        var layout = RenderLayout();

        layout.WaitForAssertion(() => layout.Find(".mud-appbar .apollo-login").TextContent.Trim().ShouldBe("Anmelden"));
        layout.FindAll(".apollo-logout").ShouldBeEmpty();
        layout.FindAll(".apollo-user-name").ShouldBeEmpty();
    }

    [Fact]
    public void AppBar_WhenAuthenticated_ShouldShowNameAndAbmelden()
    {
        UseApiClients(useMockData: false, _appInfo, _user);

        var layout = RenderLayout();

        layout.WaitForAssertion(() => layout.Find(".mud-appbar .apollo-user-name").TextContent.Trim().ShouldBe("Nelly"));
        layout.Find(".mud-appbar .apollo-logout").TextContent.Trim().ShouldBe("Abmelden");
        layout.FindAll(".apollo-login").ShouldBeEmpty();
    }

    [Fact]
    public void AppBar_WhenUsingMockClient_ShouldShowMockUser()
    {
        var layout = RenderLayout();

        layout.WaitForAssertion(() =>
            layout.Find(".apollo-user-name").TextContent.Trim().ShouldBe(MockAuthClient.User.DisplayName));
    }

    [Fact]
    public void Login_WhenClicked_ShouldReloadIntoLoginWithCurrentPageAsReturnUrl()
    {
        UseApiClients(useMockData: false, _appInfo);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/me?jahr=2026");
        var layout = RenderLayout();

        layout.WaitForElement(".apollo-login").Click();

        var entry = navigation.History.First();
        entry.Uri.ShouldBe("/auth/login?returnUrl=%2Fme%3Fjahr%3D2026");
        entry.Options.ForceLoad.ShouldBeTrue();
    }

    [Fact]
    public void Logout_WhenClicked_ShouldLogOutAndReloadHome()
    {
        var api = UseApiClients(useMockData: false, _appInfo, _user)
            .Respond("/auth/antiforgery", HttpStatusCode.OK, new AntiforgeryTokenDto("X-XSRF-TOKEN", "token"))
            .Respond("/auth/logout", HttpStatusCode.NoContent);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var layout = RenderLayout();

        layout.WaitForElement(".apollo-logout").Click();

        layout.WaitForAssertion(() => navigation.History.ShouldNotBeEmpty());
        api.Messages.ShouldContain(m => m.Method == HttpMethod.Post && m.RequestUri!.AbsolutePath == "/auth/logout");
        var entry = navigation.History.First();
        entry.Uri.ShouldBe("/");
        entry.Options.ForceLoad.ShouldBeTrue();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void Logout_WhenRequestFails_ShouldShowErrorAndStayLoggedIn(HttpStatusCode status)
    {
        UseApiClients(useMockData: false, _appInfo, _user)
            .Respond("/auth/antiforgery", HttpStatusCode.OK, new AntiforgeryTokenDto("X-XSRF-TOKEN", "token"))
            .Respond("/auth/logout", status);
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        var layout = RenderLayout();

        layout.WaitForElement(".apollo-logout").Click();

        layout.WaitForAssertion(() =>
            layout.Find(".mud-snackbar").TextContent.ShouldContain("Abmelden hat nicht geklappt. Versuch es bitte nochmal."));
        navigation.History.ShouldBeEmpty();
        layout.Find(".apollo-user-name").TextContent.Trim().ShouldBe("Nelly");
    }

    private static readonly AppInfoDto _appInfo = new("1.4.2", "Production");

    private static readonly CurrentUserDto _user = new(Guid.Parse("0b6f3d4e-1a2b-4c3d-8e9f-a0b1c2d3e4f5"), "Discord", "Nelly", AvatarUrl: null);

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
