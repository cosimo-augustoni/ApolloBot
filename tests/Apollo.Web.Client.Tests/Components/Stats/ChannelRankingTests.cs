using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Bunit;

namespace Apollo.Web.Client.Tests.Components.Stats;

public sealed class ChannelRankingTests : ApolloTestContext
{
    private static readonly ChannelActivityDto[] _channels =
    [
        new(Guid.NewGuid(), "allgemein", false, 9812, 0),
        new(Guid.NewGuid(), "Voice-Lounge", false, 410, (512 * 3600) + (24 * 60)),
        new(Guid.NewGuid(), "alt", true, 3, 0),
        new(Guid.NewGuid(), "Zocken", false, 1, 600),
        new(Guid.NewGuid(), null, false, 1, 0),
    ];

    [Fact]
    public void ChannelRanking_ByDefault_ShouldSortByVoiceTimeWithFormattedNumbers()
    {
        var ranking = RenderRanking(_channels);

        Rows(ranking).ShouldBe(
        [
            ["1", "Voice-Lounge", "410", "512 h 24 min"],
            ["2", "Zocken", "1", "10 min"],
            ["3", "allgemein", "9'812", "–"],
            ["4", "alt (gelöscht)", "3", "–"],
            ["5", "Unbekannter Kanal", "1", "–"],
        ]);
        SelectedToggle(ranking).ShouldBe("Voice-Stunden");
        ranking.FindAll(".apollo-channel-show-all").ShouldBeEmpty();
    }

    [Fact]
    public void ChannelRanking_WhenMessagesChosen_ShouldSortByMessages()
    {
        var ranking = RenderRanking(_channels);

        ranking.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Nachrichten").Click();

        ranking.WaitForAssertion(() => Rows(ranking).Select(r => r[1]).ShouldBe(["allgemein", "Voice-Lounge", "alt (gelöscht)", "Zocken", "Unbekannter Kanal"]));
        Rows(ranking).Select(r => r[0]).ShouldBe(["1", "2", "3", "4", "5"]);
    }

    [Fact]
    public void ChannelRanking_WhenStartingWithMessages_ShouldSortByMessages()
    {
        var ranking = Render<ChannelRanking>(p => p.Add(r => r.Channels, _channels).Add(r => r.InitialMetric, StatsMetric.Messages));

        Rows(ranking)[0][1].ShouldBe("allgemein");
        SelectedToggle(ranking).ShouldBe("Nachrichten");
    }

    [Fact]
    public void ChannelRanking_WhenMoreThanTop_ShouldShowAllOnRequest()
    {
        var ranking = RenderRanking([.. Enumerable.Range(1, 12).Select(i => new ChannelActivityDto(Guid.NewGuid(), $"kanal-{i}", false, 100 - i, 0))]);

        Rows(ranking).Count.ShouldBe(10);

        ranking.Find(".apollo-channel-show-all").Click();

        Rows(ranking).Count.ShouldBe(12);
        Rows(ranking)[^1][1].ShouldBe("kanal-12");
    }

    [Fact]
    public void ChannelRanking_WhenNoChannels_ShouldSayThereAreNone()
    {
        var ranking = RenderRanking([]);

        ranking.Find(".apollo-channel-ranking-empty").TextContent.Trim().ShouldBe("Keine Nachrichten und keine Voice-Zeit im gewählten Zeitraum");
        ranking.FindAll(".apollo-channel-show-all").ShouldBeEmpty();
        ranking.FindAll(".mud-toggle-item").ShouldBeEmpty();
    }

    private IRenderedComponent<ChannelRanking> RenderRanking(IReadOnlyList<ChannelActivityDto> channels) =>
        Render<ChannelRanking>(p => p.Add(r => r.Channels, channels));

    private static string SelectedToggle(IRenderedComponent<ChannelRanking> ranking) =>
        ranking.FindAll(".mud-toggle-item").Single(b => b.ClassList.Contains("mud-toggle-item-selected")).TextContent.Trim();

    private static List<string[]> Rows(IRenderedComponent<ChannelRanking> ranking) =>
        [.. ranking.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td").Select(td => td.TextContent.Trim()).ToArray())];
}
