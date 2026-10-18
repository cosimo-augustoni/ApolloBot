using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Bunit;

namespace Apollo.Web.Client.Tests.Components.Stats;

public sealed class ChannelRankingTests : ApolloTestContext
{
    [Fact]
    public void ChannelRanking_ShouldListTheChannelsInOrderWithFormattedNumbers()
    {
        var ranking = RenderRanking(
        [
            new ChannelActivityDto(Guid.NewGuid(), "allgemein", false, 9812, 0),
            new ChannelActivityDto(Guid.NewGuid(), "Voice-Lounge", false, 410, (512 * 3600) + (24 * 60)),
            new ChannelActivityDto(Guid.NewGuid(), "alt", true, 3, 0),
            new ChannelActivityDto(Guid.NewGuid(), null, false, 1, 0),
        ]);

        Rows(ranking).ShouldBe(
        [
            ["1", "allgemein", "9'812", "–"],
            ["2", "Voice-Lounge", "410", "512 h 24 min"],
            ["3", "alt (gelöscht)", "3", "–"],
            ["4", "Unbekannter Kanal", "1", "–"],
        ]);
        ranking.FindAll(".apollo-channel-show-all").ShouldBeEmpty();
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

    private IRenderedComponent<ChannelRanking> RenderRanking(IReadOnlyList<ChannelActivityDto> channels) =>
        Render<ChannelRanking>(p => p.Add(r => r.Channels, channels));

    private static List<string[]> Rows(IRenderedComponent<ChannelRanking> ranking) =>
        [.. ranking.FindAll("tbody tr").Select(row => row.QuerySelectorAll("td").Select(td => td.TextContent.Trim()).ToArray())];
}
