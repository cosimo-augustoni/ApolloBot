using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin data sanity page (#40) against <see cref="MockDataSanityClient"/>.</summary>
public sealed class DataSanityPageTests : ApolloTestContext
{
    private static readonly DateOnly _from = new(2026, 9, 19);
    private static readonly DateOnly _today = new(2026, 10, 18);

    // 18 October 2026, 14:00 in Zurich.
    private static readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    private readonly MockDataSanityClient _client = new(_time);

    public DataSanityPageTests()
    {
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton<IDataSanityClient>(_client);
    }

    [Fact]
    public void DataSanity_ShouldLoadTheDefaultRangeOfTheHost()
    {
        var page = Render<DataSanity>();

        page.WaitForAssertion(() => page.FindAll(".apollo-sanity-check").Count.ShouldBe(5));
        page.Find("h1").TextContent.Trim().ShouldBe("Datenprüfung");
        _client.Requests.ShouldBe([((DateOnly?)null, (DateOnly?)null)]);
        page.FindComponent<DateRangeSelector>().Instance.Value.ShouldBe(DateRangeSelection.Custom(_from, _today));
    }

    [Fact]
    public void DataSanity_WhenUntrackedDataWasFound_ShouldShowTheErrorTextAndTheCountsInTheErrorColour()
    {
        _client.Answer = (from, to) => MockDataSanityClient.Sample(from, to) with
        {
            UntrackedEvents = new SanityCheckDto<UntrackedRowDto>(
                2,
                [new UntrackedRowDto("message_events", "user_id", 42, new EventUserDto(Guid.NewGuid(), "Bert"), new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero))]),
            UntrackedReferences = new SanityCheckDto<UntrackedRowDto>(0, []),
        };

        var page = Render<DataSanity>();

        page.WaitForAssertion(() => page.Find(".apollo-sanity-untracked-alert").TextContent.ShouldContain("Daten von nicht erfassten Nutzern gefunden"));
        page.Find(".apollo-sanity-untracked-alert").ClassList.ShouldContain("mud-alert-text-error");
        var untracked = Check(page, "untracked-events");
        untracked.QuerySelector(".apollo-sanity-count")!.TextContent.Trim().ShouldBe("2");
        untracked.QuerySelector(".apollo-sanity-count")!.ClassList.ShouldContain("mud-error-text");
        untracked.QuerySelector(".apollo-sanity-example")!.TextContent.ShouldContain("Bert");
        untracked.QuerySelector(".apollo-sanity-example")!.TextContent.ShouldContain("message_events.user_id");
        untracked.QuerySelector(".apollo-sanity-example")!.TextContent.ShouldContain("12.10.2026 20:30");
        Check(page, "untracked-references").QuerySelector(".apollo-sanity-count")!.ClassList.ShouldNotContain("mud-error-text");
    }

    [Fact]
    public void DataSanity_WhenNoUntrackedDataWasFound_ShouldShowTheSuccessState()
    {
        _client.Answer = MockDataSanityClient.Sample;

        var page = Render<DataSanity>();

        page.WaitForAssertion(() => page.Find(".apollo-sanity-untracked-ok").TextContent.ShouldContain("Keine Daten von nicht erfassten Nutzern gefunden"));
        page.Find(".apollo-sanity-untracked-ok").ClassList.ShouldContain("mud-alert-text-success");
        page.FindAll(".apollo-sanity-untracked-alert").ShouldBeEmpty();
        Check(page, "untracked-events").QuerySelector(".apollo-sanity-count")!.ClassList.ShouldContain("mud-success-text");
    }

    [Fact]
    public void DataSanity_ShouldShowTheGapsAndSessionsWithTheirTimes()
    {
        var page = Render<DataSanity>();

        page.WaitForAssertion(() => page.FindAll(".apollo-sanity-check").Count.ShouldBe(5));
        var gaps = Check(page, "heartbeat-gaps");
        gaps.QuerySelector(".apollo-sanity-count")!.TextContent.Trim().ShouldBe("2");
        gaps.QuerySelector(".apollo-sanity-count")!.ClassList.ShouldContain("mud-warning-text");
        gaps.TextContent.ShouldContain("Abstand über 2 min");
        gaps.QuerySelector(".apollo-sanity-example")!.TextContent.ShouldContain("14.10.2026 05:12:30");
        gaps.QuerySelector(".apollo-sanity-example")!.TextContent.ShouldContain("1 h 4 min");
        Check(page, "open-sessions").QuerySelector(".apollo-sanity-example")!.TextContent.ShouldContain("offen");
        Check(page, "long-sessions").QuerySelector(".apollo-sanity-example a")!.GetAttribute("href").ShouldStartWith("admin/members/");
    }

    [Fact]
    public void DataSanity_WhenACheckFindsNothing_ShouldSayItIsClean()
    {
        _client.Answer = (from, to) => MockDataSanityClient.Sample(from, to) with { HeartbeatGaps = new SanityCheckDto<HeartbeatGapDto>(0, []) };

        var page = Render<DataSanity>();

        page.WaitForAssertion(() => Check(page, "heartbeat-gaps").TextContent.ShouldContain("Nichts gefunden"));
        Check(page, "heartbeat-gaps").QuerySelectorAll(".apollo-sanity-example").ShouldBeEmpty();
    }

    [Fact]
    public void DataSanity_WhenMoreRowsThanExamples_ShouldSayHowManyAreShown()
    {
        _client.Answer = (from, to) => MockDataSanityClient.Sample(from, to) with
        {
            HeartbeatGaps = new SanityCheckDto<HeartbeatGapDto>(
                31,
                [new HeartbeatGapDto(new DateTimeOffset(2026, 10, 14, 3, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 14, 3, 5, 0, TimeSpan.Zero))]),
        };

        var page = Render<DataSanity>();

        page.WaitForAssertion(() => Check(page, "heartbeat-gaps").TextContent.ShouldContain("Die neuesten 1 von 31"));
    }

    [Fact]
    public void DataSanity_WhenARangeIsChosen_ShouldReloadIt()
    {
        var page = Render<DataSanity>();
        page.WaitForAssertion(() => page.FindAll(".apollo-sanity-check").Count.ShouldBe(5));

        page.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Monat").Click();

        page.WaitForAssertion(() => _client.Requests[^1].ShouldBe(((DateOnly?)new DateOnly(2026, 10, 1), (DateOnly?)_today)));
    }

    [Fact]
    public void DataSanity_WhenLoadFails_ShouldShowAnError()
    {
        _client.Failure = new HttpRequestException("boom");

        var page = Render<DataSanity>();

        page.WaitForAssertion(() => page.Find(".apollo-sanity-load-failed").TextContent.ShouldContain("Die Datenprüfung konnte nicht geladen werden."));
        page.FindAll(".apollo-sanity-check").ShouldBeEmpty();
    }

    [Fact]
    public void DataSanity_WhileLoading_ShouldShowProgress()
    {
        _client.Gate = new TaskCompletionSource();

        var page = Render<DataSanity>();

        page.FindAll(".apollo-sanity-loading").ShouldHaveSingleItem();
        _client.Gate.SetResult();
        page.WaitForAssertion(() => page.FindAll(".apollo-sanity-loading").ShouldBeEmpty());
    }

    private static AngleSharp.Dom.IElement Check(IRenderedComponent<DataSanity> page, string name) => page.Find($".apollo-sanity-check-{name}");
}
