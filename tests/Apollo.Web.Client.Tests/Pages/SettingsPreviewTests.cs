using Apollo.Testing;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The settings cards when the preview fails or answers out of order (#33 review).</summary>
public sealed class SettingsPreviewTests : ApolloTestContext
{
    private const string PreviewError = "Die Vorschau konnte nicht geladen werden. Wähle die Rollen erneut aus oder lade die Seite neu.";

    private readonly ControlledPreviewClient _client = new(new MockRoleSettingsClient(TestTime.CreateProvider()));

    public SettingsPreviewTests() => Services.AddSingleton<IRoleSettingsClient>(_client);

    [Fact]
    public void SettingsPage_WhenThePreviewFailsOnLoad_ShouldShowAnErrorInBothCardsAndKeepSaveDisabled()
    {
        _client.Preview = _ => throw new HttpRequestException("Preview failed.");

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-tracking-preview-error").TextContent.Trim().ShouldBe(PreviewError));
        page.Find(".apollo-access-preview-error").TextContent.Trim().ShouldBe(PreviewError);
        page.FindAll(".apollo-tracking-preview, .apollo-access-preview").ShouldBeEmpty();
    }

    [Fact]
    public async Task SettingsPage_WhenThePreviewFailsAfterASelection_ShouldShowAnErrorAndKeepSaveDisabled()
    {
        var page = RenderPage();
        var tracking = page.WaitForComponent<MudSelect<Guid?>>();
        var access = page.FindComponent<MudSelect<Guid>>();
        _client.Preview = _ => throw new HttpRequestException("Preview failed.");

        await page.InvokeAsync(() => tracking.Instance.ValueChanged.InvokeAsync(MockRoleSettingsClient.Statistik));
        await page.InvokeAsync(() => access.Instance.SelectedValuesChanged.InvokeAsync([MockRoleSettingsClient.Moderation]));

        page.WaitForAssertion(() => page.Find(".apollo-tracking-preview-error").TextContent.Trim().ShouldBe(PreviewError));
        page.Find(".apollo-access-preview-error").TextContent.Trim().ShouldBe(PreviewError);
        page.Find(".apollo-tracking-save").HasAttribute("disabled").ShouldBeTrue();
        page.Find(".apollo-access-save").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task SettingsPage_WhenAnOlderPreviewAnswersLast_ShouldKeepThePreviewOfTheLatestSelection()
    {
        var page = RenderPage();
        var tracking = page.WaitForComponent<MudSelect<Guid?>>();
        var answers = new Dictionary<Guid, TaskCompletionSource<RolePreviewDto>>
        {
            [MockRoleSettingsClient.Statistik] = new(),
            [MockRoleSettingsClient.Mitglied] = new(),
        };
        _client.Preview = roleIds => answers[roleIds.Single()].Task;

        var first = page.InvokeAsync(() => tracking.Instance.ValueChanged.InvokeAsync(MockRoleSettingsClient.Statistik));
        var second = page.InvokeAsync(() => tracking.Instance.ValueChanged.InvokeAsync(MockRoleSettingsClient.Mitglied));
        answers[MockRoleSettingsClient.Mitglied].SetResult(new RolePreviewDto(187, 187, 3));
        await second;
        answers[MockRoleSettingsClient.Statistik].SetResult(new RolePreviewDto(168, 168, 3));
        await first;

        page.WaitForAssertion(() => page.Find(".apollo-tracking-preview").TextContent.Trim().ShouldBe("187 Mitglieder werden erfasst"));
        page.Find(".apollo-tracking-save").HasAttribute("disabled").ShouldBeFalse();
    }

    private IRenderedComponent<Settings> RenderPage()
    {
        Render<MudPopoverProvider>();
        return Render<Settings>();
    }
}
