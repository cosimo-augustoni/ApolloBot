using System.Reflection;
using ApexCharts;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Resources;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>
/// The shared magazine view of a member's stats (#38, scope D16), which <c>/me</c> (#42) reuses unchanged. It renders in
/// a bare bUnit context: MudBlazor, ApexCharts and the strings, but no API clients, auth or admin services.
/// </summary>
public sealed class MemberMagazineViewTests : BunitContext
{
    private static readonly Guid _anna = new("3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b");
    private static readonly DateOnly _from = new(2026, 10, 1);
    private static readonly DateOnly _to = new(2026, 10, 15);

    public MemberMagazineViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddApexCharts();
        Services.AddLocalization();
    }

    [Fact]
    public void MemberMagazineView_ShouldRenderFromPlainDtosWithoutAdminServices()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);

        var view = RenderView(stats);

        view.FindComponent<ActivityOverTimeChart>().Instance.Days.ShouldBeSameAs(stats.Activity.Daily);
        view.FindComponent<ChannelRanking>().Instance.Channels.ShouldBeSameAs(stats.Activity.Channels);
        view.FindComponent<ActivityHeatmap>().Instance.Heatmap.ShouldBeSameAs(stats.Activity.Heatmap);
        view.FindComponent<ActivityHeatmap>().Instance.VoiceHeatmap.ShouldBeSameAs(stats.Activity.VoiceHeatmap);
        view.FindAll(".apollo-stat-label").Select(l => l.TextContent.Trim())
            .ShouldBe(["Voice-Stunden", "Nachrichten", "Reaktionen", "Aktive Tage", "Längste Voice-Session"]);
        view.FindAll(".apollo-member-emojis li").ShouldNotBeEmpty();
        view.FindAll(".apollo-member-buddies li").ShouldNotBeEmpty();
        view.FindAll("a[href*='admin']").ShouldBeEmpty();
    }

    [Fact]
    public void MemberMagazineView_ShouldInjectNothingButTheStrings()
    {
        var injected = typeof(MemberMagazineView)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null)
            .Select(p => p.PropertyType);

        injected.ShouldBe([typeof(IStringLocalizer<Strings>)]);
    }

    [Fact]
    public void MemberMagazineView_ShouldShowTheTilesOfTheRange()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);
        stats = stats with
        {
            Activity = stats.Activity with
            {
                Totals = new ActivityCountsDto(Messages: 1612, Characters: 0, ReactionsGiven: 148, ReactionsReceived: 90, VoiceSeconds: (19 * 3600) + (30 * 60)),
                Averages = new ActivityAveragesDto(Messages: 107.47, Characters: 0, ReactionsGiven: 9.87, ReactionsReceived: 0, VoiceSeconds: 4680),
                ActiveDays = 12,
                TrackedDays = 15,
            },
        };

        var view = RenderView(stats);

        Tile(view, "Nachrichten").ShouldBe(("1'612", "Ø 107.5 pro Tag"));
        Tile(view, "Voice-Stunden").ShouldBe(("19 h 30 min", "Ø 1 h 18 min pro Tag"));
        Tile(view, "Reaktionen").ShouldBe(("148", "Ø 9.9 pro Tag"));
        Tile(view, "Aktive Tage").ShouldBe(("12 von 15", "Erfasste Tage im Zeitraum"));
    }

    [Fact]
    public void MemberMagazineView_ShouldShowTopEmojisVoiceBuddiesAndTheLongestSession()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);
        stats = stats with
        {
            Highlights = stats.Highlights with
            {
                TopEmojis = [new EmojiUsageDto("😂", false, 41), new EmojiUsageDto("apollo", true, 6), new EmojiUsageDto("", true, 2)],
                VoiceBuddies = [new VoiceBuddyDto(new EventUserDto(Guid.NewGuid(), "Ben"), (6 * 3600) + (12 * 60)), new VoiceBuddyDto(new EventUserDto(Guid.NewGuid(), null), 1800)],
                LongestVoiceSession = new LongestVoiceSessionDto(
                    new EventChannelDto(Guid.NewGuid(), "Voice-Lounge"),
                    new DateTimeOffset(2026, 10, 7, 18, 30, 0, TimeSpan.Zero),
                    (4 * 3600) + (37 * 60)),
            },
        };

        var view = RenderView(stats);

        Rows(view, ".apollo-member-emojis").ShouldBe([["😂", "41"], [":apollo:", "6"], ["Unbekanntes Emoji", "2"]]);
        Rows(view, ".apollo-member-buddies").ShouldBe([["Ben", "6 h 12 min"], ["Unbekanntes Mitglied", "30 min"]]);
        var longest = view.Find(".apollo-member-longest");
        longest.QuerySelector(".apollo-stat-value")!.TextContent.Trim().ShouldBe("4 h 37 min");
        longest.QuerySelector(".apollo-stat-subtext")!.TextContent.Trim().ShouldBe("in Voice-Lounge am 07.10.2026 20:30");
        view.Find("#voice .apollo-chapter-key-sub").TextContent.Trim().ShouldBe("im Voice, am meisten mit Ben");
    }

    [Fact]
    public void MemberMagazineView_ShouldShowWhomTheMemberRepliesToAndWhoRepliesToThem()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);
        stats = stats with
        {
            Highlights = stats.Highlights with
            {
                RepliesTo = [new ReplyPartnerDto(new EventUserDto(Guid.NewGuid(), "Ben"), 34)],
                RepliedBy = [new ReplyPartnerDto(new EventUserDto(Guid.NewGuid(), "Chris"), 1029)],
            },
        };

        var view = RenderView(stats);

        Rows(view, ".apollo-member-replies-to").ShouldBe([["Ben", "34"]]);
        Rows(view, ".apollo-member-replied-by").ShouldBe([["Chris", "1'029"]]);
    }

    [Fact]
    public void MemberMagazineView_ShouldLeadTheChaptersWithTheirKeyNumber()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);
        stats = stats with
        {
            Activity = stats.Activity with
            {
                Totals = stats.Activity.Totals with { Messages = 200 },
                Channels = [new ChannelActivityDto(Guid.NewGuid(), "allgemein", false, 82, 0), new ChannelActivityDto(Guid.NewGuid(), "gaming", false, 18, 0)],
                Heatmap = Heatmap((2, 21, 9), (5, 14, 3)),
                VoiceHeatmap = Heatmap((4, 20, 3600), (6, 22, 1800)),
            },
        };

        var view = RenderView(stats);

        Key(view, "kanaele").ShouldBe(("#allgemein", "41 % deiner Nachrichten"));
        Key(view, "zeiten").ShouldBe(("Fr, 20 Uhr", "deine aktivste Stunde im Voice"));
        HeatmapMetric(view).ShouldBe("Voice-Zeit");
    }

    [Fact]
    public void MemberMagazineView_WhenOnlyVoiceTime_ShouldShowTheTimesWithTheVoiceHeatmap()
    {
        var empty = MockMemberStatsClient.NeverTracked(_anna, _from, _to);
        var stats = empty with
        {
            Activity = empty.Activity with
            {
                Totals = empty.Activity.Totals with { VoiceSeconds = 5400 },
                ActiveDays = 1,
                VoiceHeatmap = Heatmap((0, 21, 1200), (0, 22, 3600), (0, 23, 600)),
            },
        };

        var view = RenderView(stats);

        Key(view, "zeiten").ShouldBe(("Mo, 22 Uhr", "deine aktivste Stunde im Voice"));
        HeatmapMetric(view).ShouldBe("Voice-Zeit");
        view.FindComponent<ActivityOverTimeChart>().FindComponent<ApexPointSeries<DailyActivityDto>>().Instance.Name.ShouldBe("Voice-Stunden");
    }

    [Fact]
    public void MemberMagazineView_WhenOnlyMessages_ShouldShowTheTimesWithTheMessageHeatmap()
    {
        var empty = MockMemberStatsClient.NeverTracked(_anna, _from, _to);
        var stats = empty with
        {
            Activity = empty.Activity with
            {
                Totals = empty.Activity.Totals with { Messages = 12 },
                ActiveDays = 1,
                Channels = [new ChannelActivityDto(Guid.NewGuid(), "allgemein", false, 12, 0)],
                Heatmap = Heatmap((2, 9, 10), (3, 18, 2)),
            },
        };

        var view = RenderView(stats);

        Key(view, "zeiten").ShouldBe(("Mi, 09 Uhr", "deine aktivste Stunde"));
        HeatmapMetric(view).ShouldBe("Nachrichten");
        view.FindComponent<ActivityOverTimeChart>().FindComponent<ApexPointSeries<DailyActivityDto>>().Instance.Name.ShouldBe("Nachrichten");
    }

    [Fact]
    public void MemberMagazineView_WhenGameData_ShouldShowTheGamesChapter()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);
        stats = stats with { Highlights = stats.Highlights with { TopGames = [new GamePlaytimeDto("Factorio", (8 * 3600) + (6 * 60)), new GamePlaytimeDto("Tetris", 600)] } };

        var view = RenderView(stats);

        view.Find("#spiele h2").TextContent.Trim().ShouldBe("Spiele");
        Rows(view, ".apollo-member-games").ShouldBe([["Factorio", "8 h 6 min"], ["Tetris", "10 min"]]);
        Key(view, "spiele").ShouldBe(("8 h 6 min", "Factorio"));
    }

    [Fact]
    public void MemberMagazineView_WhenNoGameData_ShouldLeaveTheGamesChapterOut()
    {
        var stats = MockMemberStatsClient.Sample(_anna, _from, _to);
        stats = stats with { Highlights = stats.Highlights with { TopGames = [] } };

        var view = RenderView(stats);

        view.FindAll("#spiele").ShouldBeEmpty();
        view.Markup.ShouldNotContain("Spiele");
    }

    [Fact]
    public void MemberMagazineView_WhenTheRangeHasNoData_ShouldShowTheEmptyStateInsteadOfCharts()
    {
        var stats = MockMemberStatsClient.NeverTracked(_anna, _from, _to);

        var view = RenderView(stats);

        view.Find(".apollo-stats-empty .apollo-empty-title").TextContent.Trim().ShouldBe("Keine Daten im gewählten Zeitraum");
        view.FindComponents<ActivityOverTimeChart>().ShouldBeEmpty();
        view.FindComponents<ChannelRanking>().ShouldBeEmpty();
        view.FindComponents<ActivityHeatmap>().ShouldBeEmpty();
        view.FindAll(".apollo-chapter").ShouldBeEmpty();
    }

    [Fact]
    public void MemberMagazineView_WhenTheMemberOnlyReacted_ShouldSayTheRankingIsEmptyAndLeaveTheEmptyChaptersOut()
    {
        var empty = MockMemberStatsClient.NeverTracked(_anna, _from, _to);
        var stats = empty with
        {
            Activity = empty.Activity with { Totals = empty.Activity.Totals with { ReactionsGiven = 3 }, ActiveDays = 1 },
            Highlights = empty.Highlights with { TopEmojis = [new EmojiUsageDto("👍", false, 3)] },
        };

        var view = RenderView(stats);

        view.Find(".apollo-channel-ranking-empty").TextContent.Trim().ShouldBe("Keine Nachrichten und keine Voice-Zeit im gewählten Zeitraum");
        view.FindAll("#kanaele .apollo-chapter-key").ShouldBeEmpty();
        Rows(view, ".apollo-member-emojis").ShouldBe([["👍", "3"]]);
        view.FindAll("#voice").ShouldBeEmpty();
        view.FindAll("#zeiten").ShouldBeEmpty();
        view.FindComponents<ActivityHeatmap>().ShouldBeEmpty();
    }

    private IRenderedComponent<MemberMagazineView> RenderView(MemberStatsDto stats) =>
        Render<MemberMagazineView>(p => p
            .Add(v => v.Activity, stats.Activity)
            .Add(v => v.Highlights, stats.Highlights));

    /// <summary>A 7 × 24 heatmap, zero but for the given (weekday, hour, value) cells.</summary>
    private static IReadOnlyList<IReadOnlyList<long>> Heatmap(params (int Day, int Hour, long Value)[] cells)
    {
        var heatmap = Enumerable.Range(0, 7).Select(_ => new long[24]).ToArray();
        foreach (var (day, hour, value) in cells)
        {
            heatmap[day][hour] = value;
        }

        return heatmap;
    }

    /// <summary>The metric the heatmap's caption names ("Voice-Zeit pro Stunde"); the bare context has no breakpoint.</summary>
    private static string HeatmapMetric(IRenderedComponent<MemberMagazineView> view) =>
        view.Find("#zeiten .apollo-heatmap-caption").TextContent.Trim().Split(" pro ")[0];

    private static (string Value, string SubText) Tile(IRenderedComponent<MemberMagazineView> view, string label)
    {
        var tile = view.FindAll(".apollo-stat-tile").Single(t => t.QuerySelector(".apollo-stat-label")!.TextContent.Trim() == label);
        return (tile.QuerySelector(".apollo-stat-value")!.TextContent.Trim(), tile.QuerySelector(".apollo-stat-subtext")!.TextContent.Trim());
    }

    private static (string Value, string SubText) Key(IRenderedComponent<MemberMagazineView> view, string chapter) =>
        (view.Find($"#{chapter} .apollo-chapter-key-value").TextContent.Trim(), view.Find($"#{chapter} .apollo-chapter-key-sub").TextContent.Trim());

    private static List<string[]> Rows(IRenderedComponent<MemberMagazineView> view, string list) =>
        [.. view.FindAll($"{list} li").Select(li => new[]
        {
            li.QuerySelector(".apollo-rank-label")!.TextContent.Trim(),
            li.QuerySelector(".apollo-rank-value")!.TextContent.Trim(),
        })];
}
