using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin member stats page (#38) against <see cref="MockMemberStatsClient"/>.</summary>
public sealed class MemberStatsPageTests : ApolloTestContext
{
    // 18 October 2026, 14:00 in Zurich.
    private static readonly DateOnly _today = new(2026, 10, 18);
    private static readonly Guid _anna = new("3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b");

    private readonly MockMemberStatsClient _client = new();

    public MemberStatsPageTests()
    {
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)));
        Services.AddSingleton<IMemberStatsClient>(_client);
    }

    [Fact]
    public void MemberStats_ShouldLoadTheMembersCurrentMonth()
    {
        var page = RenderPage();

        page.WaitForAssertion(() => page.FindComponents<MemberMagazineView>().ShouldHaveSingleItem());
        _client.Requests.ShouldBe([(_anna, new DateOnly(2026, 10, 1), _today)]);
        page.Find(".apollo-range-text").TextContent.Trim().ShouldBe("01.10.2026 – 18.10.2026");
    }

    [Fact]
    public void MemberStats_WhenTracked_ShouldShowNameAndTrackedSince()
    {
        var page = RenderPage();

        page.WaitForAssertion(() => page.Find("h1").TextContent.Trim().ShouldBe("Anna"));
        page.Find(".apollo-member-status").TextContent.Trim().ShouldBe("Erfasst");
        page.Find(".apollo-member-status-text").TextContent.Trim().ShouldBe("seit 03.02.2025 · Discord");
    }

    [Fact]
    public void MemberStats_WhenNoLongerTracked_ShouldShowNotTrackedAndUntilWhen()
    {
        _client.Answer = (id, from, to) => MockMemberStatsClient.Sample(id, from, to) with
        {
            Tracking = new TrackingStatusDto(TrackedSince: null, TrackedUntil: new DateOnly(2026, 8, 12)),
        };

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-member-status").TextContent.Trim().ShouldBe("Nicht erfasst"));
        page.Find(".apollo-member-status-text").TextContent.Trim().ShouldBe("bis 12.08.2026 · Discord");
        page.FindComponents<MemberMagazineView>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MemberStats_ShouldPassTheStatsToTheMagazineView()
    {
        var expected = MockMemberStatsClient.Sample(_anna, new DateOnly(2026, 10, 1), _today);
        _client.Answer = (_, _, _) => expected;

        var page = RenderPage();

        page.WaitForAssertion(() => page.FindComponent<MemberMagazineView>().Instance.Activity.ShouldBeSameAs(expected.Activity));
        page.FindComponent<MemberMagazineView>().Instance.Highlights.ShouldBeSameAs(expected.Highlights);
    }

    [Fact]
    public void MemberStats_WhenNeverTracked_ShouldShowAnEmptyStateInsteadOfCharts()
    {
        _client.Answer = MockMemberStatsClient.NeverTracked;

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-member-never-tracked .apollo-empty-title").TextContent.Trim()
            .ShouldBe("Für Anna werden keine Daten erfasst"));
        page.Find(".apollo-member-status").TextContent.Trim().ShouldBe("Nicht erfasst");
        page.FindComponents<MemberMagazineView>().ShouldBeEmpty();
        page.FindComponents<ActivityOverTimeChart>().ShouldBeEmpty();
        page.FindComponents<DateRangeSelector>().ShouldBeEmpty();
    }

    [Fact]
    public void MemberStats_WhenTheMemberIsUnknown_ShouldSaySo()
    {
        _client.Answer = (_, _, _) => null;

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-member-not-found .apollo-empty-title").TextContent.Trim().ShouldBe("Mitglied nicht gefunden"));
        page.FindComponents<MemberMagazineView>().ShouldBeEmpty();
    }

    [Fact]
    public void MemberStats_WhenLoadFails_ShouldShowAnError()
    {
        _client.Failure = new HttpRequestException("boom");

        var page = RenderPage();

        page.WaitForAssertion(() => page.Find(".apollo-stats-load-failed").TextContent.ShouldContain("konnte nicht geladen werden"));
        page.FindComponents<MemberMagazineView>().ShouldBeEmpty();
    }

    [Fact]
    public void MemberStats_WhileLoading_ShouldShowProgress()
    {
        _client.Gate = new TaskCompletionSource();

        var page = RenderPage();

        page.FindAll(".apollo-stats-loading").ShouldHaveSingleItem();
        _client.Gate.SetResult();
        page.WaitForAssertion(() => page.FindAll(".apollo-stats-loading").ShouldBeEmpty());
        page.FindComponents<MemberMagazineView>().ShouldHaveSingleItem();
    }

    [Fact]
    public void MemberStats_WhenYearChosen_ShouldReloadTheYearAndKeepTheHeader()
    {
        var page = RenderPage();
        page.WaitForAssertion(() => page.FindComponents<MemberMagazineView>().ShouldHaveSingleItem());
        _client.Gate = new TaskCompletionSource();

        page.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Jahr").Click();

        page.WaitForAssertion(() => _client.Requests[^1].ShouldBe((_anna, new DateOnly(2026, 1, 1), _today)));
        page.Find("h1").TextContent.Trim().ShouldBe("Anna");
        _client.Gate.SetResult();
        page.WaitForAssertion(() => page.FindComponent<MemberMagazineView>().Instance.Activity.From.ShouldBe(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void MemberStats_WhenTheUserIdChanges_ShouldLoadTheOtherMember()
    {
        var page = RenderPage();
        page.WaitForAssertion(() => page.FindComponents<MemberMagazineView>().ShouldHaveSingleItem());
        var ben = Guid.NewGuid();

        page.Render(p => p.Add(m => m.UserId, ben));

        page.WaitForAssertion(() => _client.Requests[^1].UserId.ShouldBe(ben));
    }

    private IRenderedComponent<MemberStats> RenderPage() => Render<MemberStats>(p => p.Add(m => m.UserId, _anna));
}
