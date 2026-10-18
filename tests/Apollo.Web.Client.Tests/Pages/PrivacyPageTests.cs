using System.Reflection;
using Apollo.Web.Client.Pages;
using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace Apollo.Web.Client.Tests.Pages;

public sealed class PrivacyPageTests : ApolloTestContext
{
    [Fact]
    public void Privacy_ShouldBeRoutedAtDatenschutzWithoutAuthorization()
    {
        var page = typeof(Privacy);

        page.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ShouldBe(["/datenschutz"]);
        page.GetCustomAttributes<AuthorizeAttribute>().ShouldBeEmpty();
        page.GetCustomAttributes<AllowAnonymousAttribute>().ShouldNotBeEmpty();
    }

    [Fact]
    public void Render_ShouldStateThatMessageContentIsNeverStored()
    {
        var page = Render<Privacy>();

        page.Find(".apollo-privacy-content-never-stored").TextContent.ShouldContain("Inhalt deiner Nachrichten wird nie gespeichert");
    }

    [Fact]
    public void Render_ShouldNameTheFourCommands()
    {
        var page = Render<Privacy>();

        page.FindAll("code").Select(c => c.TextContent.Trim()).ShouldBe(
            ["/apollo tracking off", "/apollo tracking on", "/apollo tracking status", "/apollo privacy delete"],
            ignoreOrder: true);
    }

    [Fact]
    public void Render_ShouldExplainThatTrackingOffKeepsTheDataAndPrivacyDeleteRemovesItPermanently()
    {
        var page = Render<Privacy>();

        CommandItem(page, "/apollo tracking off").ShouldContain("Bereits erfasste Daten bleiben gespeichert");
        CommandItem(page, "/apollo privacy delete").ShouldContain("endgültig");
        CommandItem(page, "/apollo tracking on").ShouldContain("wieder");
        CommandItem(page, "/apollo tracking status").ShouldContain("seit wann");
    }

    [Fact]
    public void Render_ShouldListWhatIsRecorded()
    {
        var page = Render<Privacy>();

        var recorded = page.FindAll(".apollo-privacy-recorded li").Select(li => li.TextContent).ToList();

        recorded.Count.ShouldBe(5);
        recorded[0].ShouldStartWith("Nachrichten:");
        recorded[1].ShouldStartWith("Voice:");
        recorded[2].ShouldStartWith("Reaktionen:");
        recorded[3].ShouldStartWith("Beitritte");
        recorded[4].ShouldStartWith("Aktivitäten (nur Discord):");
    }

    [Fact]
    public void Render_ShouldSayThatOnlyHoldersOfTheTrackingRoleAreRecorded()
    {
        var page = Render<Privacy>();

        var section = page.Find(".apollo-privacy-tracking-role").TextContent;

        section.ShouldContain("Apollo erfasst nur Mitglieder mit der Tracking-Rolle");
        section.ShouldContain("nicht, wen sie betrifft");
    }

    [Fact]
    public void Render_ShouldSayWhoSeesTheDataAndThatTimesAreEvaluatedInZurich()
    {
        var page = Render<Privacy>();

        page.Find(".apollo-privacy-visibility").TextContent.ShouldContain("Administrator");
        page.Find(".apollo-privacy-recorded").TextContent.ShouldContain("Europe/Zurich");
    }

    [Fact]
    public void Render_ShouldContainNoControls()
    {
        var page = Render<Privacy>();

        page.FindAll("button, input, select, textarea, form, a").ShouldBeEmpty();
    }

    private static string CommandItem(IRenderedComponent<Privacy> page, string command) =>
        page.FindAll("li").Single(li => li.QuerySelector("code")?.TextContent.Trim() == command).TextContent;
}
