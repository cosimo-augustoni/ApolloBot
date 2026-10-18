using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpServerStatsClientTests
{
    [Fact]
    public async Task GetAsync_ShouldReadTheStatsOfTheRange()
    {
        var counts = new ActivityCountsDto(Messages: 12, Characters: 340, ReactionsGiven: 3, ReactionsReceived: 2, VoiceSeconds: 5400);
        var channel = new ChannelActivityDto(Guid.NewGuid(), "allgemein", IsDeleted: true, Messages: 12, VoiceSeconds: 0);
        IReadOnlyList<long>[] heatmap = [.. Enumerable.Range(0, 7).Select(d => (IReadOnlyList<long>)[.. Enumerable.Range(0, 24).Select(h => (long)(d * h))])];
        var expected = new ActivityStatsDto(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 15),
            counts,
            new ActivityAveragesDto(0.8, 22.7, 0.2, 0.1, 360),
            ActiveDays: 4,
            ActiveUsers: 3,
            TrackedDays: null,
            TrackedSince: null,
            [new DailyActivityDto(new DateOnly(2026, 1, 1), counts)],
            [channel],
            heatmap);
        var handler = new StubHttpMessageHandler(expected);

        var stats = await new HttpServerStatsClient(handler.CreateClient())
            .GetAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), TestContext.Current.CancellationToken);

        stats.Daily.ShouldBe(expected.Daily);
        stats.Channels.ShouldBe(expected.Channels);
        stats.Heatmap.Select(d => d.ToArray()).ShouldBe(heatmap.Select(d => d.ToArray()));
        (stats with { Daily = expected.Daily, Channels = expected.Channels, Heatmap = heatmap }).ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/stats/server?from=2026-01-01&to=2026-01-15");
    }
}
