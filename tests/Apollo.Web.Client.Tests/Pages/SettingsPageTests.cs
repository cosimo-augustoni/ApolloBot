using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin;
using Apollo.Web.Client.Pages.Admin.RoleSettings;
using Apollo.Testing;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin settings page (#33) against <see cref="MockRoleSettingsClient"/>.</summary>
public sealed class SettingsPageTests : ApolloTestContext
{
    private readonly MockRoleSettingsClient _client = new(TestTime.CreateProvider());
    private IRenderedComponent<MudPopoverProvider>? _popovers;

    private IRenderedComponent<MudPopoverProvider> Popovers => _popovers ?? throw new InvalidOperationException("Render the page first.");

    public SettingsPageTests() => Services.AddSingleton<IRoleSettingsClient>(_client);

    [Fact]
    public void SettingsPage_ShouldRenderOneTabPerPlatform()
    {
        _client.Platforms = ["Discord", "Fluxer"];

        var page = RenderPage();

        page.WaitForAssertion(() => page.FindComponents<MudTabPanel>().Select(t => t.Instance.Text).ShouldBe(["Discord", "Fluxer"]));
        page.FindComponents<RoleSettingsPanel>().Select(p => p.Instance.Platform).ShouldBe(["Discord", "Fluxer"]);
    }

    [Fact]
    public void SettingsPage_WhenNoPlatformIsSynced_ShouldExplainIt()
    {
        _client.Platforms = [];

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Noch keine Plattform verbunden"));
        page.FindComponents<MudTabs>().ShouldBeEmpty();
    }

    [Fact]
    public async Task SettingsPage_ShouldOfferTheNonDeletedRolesWithColourAndMemberCount()
    {
        var page = RenderPage();
        var select = page.WaitForComponent<MudSelect<Guid?>>();

        await page.InvokeAsync(select.Instance.OpenMenu);

        var labels = Popovers.FindComponents<RoleLabel>().Select(l => l.Instance.Role).Distinct().ToList();
        labels.Select(r => r.Name).ShouldBe(["Admin", "Moderation", "Statistik", "Mitglied", "Stammgast"]);
        var statistik = Popovers.FindComponents<RoleLabel>().First(l => l.Instance.Role.Name == "Statistik");
        statistik.Find(".apollo-role-dot").GetAttribute("style").ShouldBe("background-color: #3498DB");
        statistik.Markup.ShouldContain("168 Mitglieder");
    }

    [Fact]
    public void SettingsPage_WhenBootstrap_ShouldShowBanner()
    {
        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-settings-bootstrap").TextContent.ShouldContain("Bootstrap-Modus"));
        page.FindAll(".apollo-settings-deleted").ShouldBeEmpty();
    }

    [Fact]
    public void SettingsPage_WhenTrackingRoleIsActive_ShouldShowNoModeBanner()
    {
        _client.Settings = _client.Settings with { TrackingRoleId = MockRoleSettingsClient.Statistik, Mode = RoleSettingsMode.Active };

        var page = RenderPage();

        page.WaitForComponent<RoleSettingsPanel>();
        page.WaitForAssertion(() => page.FindComponents<MudSelect<Guid?>>().ShouldNotBeEmpty());
        page.FindAll(".apollo-settings-bootstrap, .apollo-settings-deleted, .apollo-settings-cleared").ShouldBeEmpty();
    }

    [Fact]
    public void SettingsPage_WhenTrackingRoleDeleted_ShouldShowWarning()
    {
        _client.Settings = _client.Settings with { TrackingRoleId = MockRoleSettingsClient.Alt, Mode = RoleSettingsMode.TrackingRoleDeleted };

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-settings-deleted").TextContent.ShouldContain("niemand wird erfasst"));
        page.FindAll(".apollo-settings-bootstrap").ShouldBeEmpty();
    }

    [Fact]
    public void SettingsPage_WhenTrackingRoleCleared_ShouldWarnThatNobodyIsTracked()
    {
        _client.Settings = _client.Settings with { Mode = RoleSettingsMode.TrackingRoleCleared };

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-settings-cleared").TextContent.ShouldContain("auch nicht Administratoren"));
    }

    [Fact]
    public async Task SettingsPage_WhenRoleSelected_ShouldShowPreview()
    {
        var page = RenderPage();
        var select = page.WaitForComponent<MudSelect<Guid?>>();

        await page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(MockRoleSettingsClient.Statistik));

        page.WaitForAssertion(() => page.Find(".apollo-tracking-preview").TextContent.Trim().ShouldBe("168 Mitglieder werden erfasst"));
    }

    [Fact]
    public void SettingsPage_InBootstrapMode_ShouldPreviewTheAdministratorsBeforeAnythingIsSelected()
    {
        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-tracking-preview").TextContent.Trim().ShouldBe("Zurzeit: 3 Administratoren werden erfasst"));
    }

    [Fact]
    public async Task SettingsPage_WhenMemberAccessRolesSelected_ShouldShowPreviewAndSaveThem()
    {
        var page = RenderPage();
        var select = page.WaitForComponent<MudSelect<Guid>>();

        await page.InvokeAsync(() => select.Instance.SelectedValuesChanged.InvokeAsync([MockRoleSettingsClient.Mitglied, MockRoleSettingsClient.Moderation]));
        page.WaitForAssertion(() => page.Find(".apollo-access-preview").TextContent.Trim().ShouldBe("192 Mitglieder haben Zugriff"));
        page.Find(".apollo-access-save").Click();

        page.WaitForAssertion(() => _client.Settings.MemberAccessRoleIds.ShouldBe([MockRoleSettingsClient.Mitglied, MockRoleSettingsClient.Moderation], ignoreOrder: true));
    }

    [Fact]
    public void SettingsPage_WhenBotLacksManageRoles_ShouldShowThePermissionWarning()
    {
        _client.Settings = _client.Settings with { BotPermissions = new BotPermissionCheckDto(CanManageRoles: false, IsAboveTrackingRole: null) };

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-settings-bot-warning").TextContent.ShouldContain("«Rollen verwalten»"));
    }

    [Fact]
    public void SettingsPage_WhenBotRoleIsBelowTheTrackingRole_ShouldShowTheHierarchyWarning()
    {
        _client.Settings = _client.Settings with
        {
            TrackingRoleId = MockRoleSettingsClient.Statistik,
            Mode = RoleSettingsMode.Active,
            BotPermissions = new BotPermissionCheckDto(CanManageRoles: true, IsAboveTrackingRole: false),
        };

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-settings-bot-warning").TextContent.ShouldContain("über «Statistik» stehen"));
    }

    [Fact]
    public void SettingsPage_WhenBotPermissionsAreOk_ShouldShowNoWarning()
    {
        _client.Settings = _client.Settings with
        {
            TrackingRoleId = MockRoleSettingsClient.Statistik,
            Mode = RoleSettingsMode.Active,
            BotPermissions = new BotPermissionCheckDto(CanManageRoles: true, IsAboveTrackingRole: true),
        };

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-settings-bot-ok").TextContent.ShouldContain("Bot-Rolle steht über «Statistik»"));
        page.FindAll(".apollo-settings-bot-warning").ShouldBeEmpty();
    }

    [Fact]
    public void SettingsPage_ShouldShowTheHistoryNewestFirstWithGermanDates()
    {
        _client.History =
        [
            new(new DateTimeOffset(2026, 10, 15, 8, 30, 0, TimeSpan.Zero), "Alice", RoleSettingKind.Tracking, RoleSettingChangeAction.Added, "Statistik"),
            new(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero), null, RoleSettingKind.MemberAccess, RoleSettingChangeAction.Removed, "Gast"),
        ];

        var page = RenderPage();

        page.WaitForAssertion(() => page.FindAll(".apollo-history-row").Count.ShouldBe(2));
        var rows = page.FindAll(".apollo-history-row").Select(r => r.QuerySelectorAll("td").Select(c => c.TextContent.Trim()).ToList()).ToList();
        rows[0].ShouldBe(["15.10.2026 10:30", "Alice", "Tracking-Rolle festgelegt: Statistik"]);
        rows[1].ShouldBe(["15.01.2026 10:00", "–", "Zugriffsrolle entfernt: Gast"]);
    }

    [Fact]
    public void SettingsPage_ShouldAskTheHostForThe20NewestChanges()
    {
        _client.History = [.. Enumerable.Range(0, 25).Select(i => new RoleSettingChangeDto(
            TestTime.Now.AddMinutes(-i), "Anna", RoleSettingKind.MemberAccess, RoleSettingChangeAction.Added, $"Rolle {i}"))];

        var page = RenderPage();

        page.WaitForAssertion(() => page.FindAll(".apollo-history-row").Count.ShouldBe(20));
    }

    [Fact]
    public async Task SettingsPage_WhenSavingTrackingRole_ShouldConfirmTheEffectThenSave()
    {
        var dialogs = Render<MudDialogProvider>();
        var page = RenderPage();
        var select = page.WaitForComponent<MudSelect<Guid?>>();
        await page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(MockRoleSettingsClient.Statistik));
        page.WaitForAssertion(() => page.Find(".apollo-tracking-preview").TextContent.ShouldContain("168"));

        var saving = page.Find(".apollo-tracking-save").ClickAsync(new());

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("3 Mitglieder werden nicht mehr erfasst"));
        dialogs.Markup.ShouldContain("Bootstrap-Modus endet");
        _client.Settings.TrackingRoleId.ShouldBeNull();
        await dialogs.FindAll("button").Single(b => b.TextContent.Trim() == "Bestätigen").ClickAsync(new());
        await saving;
        page.WaitForAssertion(() => _client.Settings.TrackingRoleId.ShouldBe(MockRoleSettingsClient.Statistik));
        page.WaitForAssertion(() => page.Find(".apollo-history-row td:last-child").TextContent.Trim().ShouldBe("Tracking-Rolle festgelegt: Statistik"));
    }

    [Fact]
    public async Task SettingsPage_WhenClearingTheTrackingRole_ShouldWarnThatNothingIsRecordedAnymore()
    {
        _client.Settings = _client.Settings with { TrackingRoleId = MockRoleSettingsClient.Statistik, Mode = RoleSettingsMode.Active };
        var dialogs = Render<MudDialogProvider>();
        var page = RenderPage();
        var select = page.WaitForComponent<MudSelect<Guid?>>();
        await page.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(null));

        _ = page.Find(".apollo-tracking-save").ClickAsync(new());

        dialogs.WaitForAssertion(() => dialogs.Markup.ShouldContain("Danach werden keine Daten mehr erfasst – auch nicht für Administratoren."));
    }

    [Fact]
    public void SettingsPage_SelectsShouldUseTheFullWidth()
    {
        var page = RenderPage();

        page.WaitForComponent<MudSelect<Guid?>>().Instance.FullWidth.ShouldBeTrue();
        page.FindComponent<MudSelect<Guid>>().Instance.FullWidth.ShouldBeTrue();
    }

    private IRenderedComponent<Settings> RenderPage()
    {
        _popovers = Render<MudPopoverProvider>();
        return Render<Settings>();
    }
}
