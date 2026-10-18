using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpDataSanityClientTests
{
    [Fact]
    public async Task GetAsync_WithARange_ShouldReadTheReportOfTheRange()
    {
        var expected = MockDataSanityClient.Sample(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        var handler = new StubHttpMessageHandler(expected);

        var report = await new HttpDataSanityClient(handler.CreateClient())
            .GetAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), TestContext.Current.CancellationToken);

        report.HeartbeatGaps.Examples.ShouldBe(expected.HeartbeatGaps.Examples);
        report.UntrackedEvents.Examples.ShouldBe(expected.UntrackedEvents.Examples);
        (report.From, report.To, report.LongSessions.Count).ShouldBe((expected.From, expected.To, expected.LongSessions.Count));
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/sanity?from=2026-01-01&to=2026-01-15");
    }

    [Fact]
    public async Task GetAsync_WithoutARange_ShouldLeaveTheDefaultToTheHost()
    {
        var handler = new StubHttpMessageHandler(MockDataSanityClient.Sample(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 30)));

        await new HttpDataSanityClient(handler.CreateClient()).GetAsync(null, null, TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/sanity");
    }
}
