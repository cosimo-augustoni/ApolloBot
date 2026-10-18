using System.Net;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpMemberStatsClientTests
{
    private static readonly Guid _anna = new("3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b");

    [Fact]
    public async Task GetAsync_ShouldReadTheMembersStatsOfTheRange()
    {
        var expected = MockMemberStatsClient.Sample(_anna, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        var handler = new StubHttpMessageHandler(expected);

        var stats = await new HttpMemberStatsClient(handler.CreateClient())
            .GetAsync(_anna, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), TestContext.Current.CancellationToken);

        stats.ShouldNotBeNull();
        stats.Member.ShouldBe(expected.Member);
        stats.Tracking.ShouldBe(expected.Tracking);
        stats.Activity.Totals.ShouldBe(expected.Activity.Totals);
        stats.Highlights.VoiceBuddies.ShouldBe(expected.Highlights.VoiceBuddies);
        stats.Highlights.LongestVoiceSession.ShouldBe(expected.Highlights.LongestVoiceSession);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery
            .ShouldBe("/api/admin/members/3f2a1b4c-5d6e-4f70-8a9b-0c1d2e3f4a5b/stats?from=2026-01-01&to=2026-01-15");
    }

    [Fact]
    public async Task GetAsync_WhenTheMemberIsUnknown_ShouldReturnNull()
    {
        var handler = new StubHttpMessageHandler(new object())
            .Respond($"/api/admin/members/{_anna}/stats", HttpStatusCode.NotFound);

        var stats = await new HttpMemberStatsClient(handler.CreateClient())
            .GetAsync(_anna, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), TestContext.Current.CancellationToken);

        stats.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_WhenTheHostFails_ShouldThrow()
    {
        var handler = new StubHttpMessageHandler(new object())
            .Respond($"/api/admin/members/{_anna}/stats", HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(() => new HttpMemberStatsClient(handler.CreateClient())
            .GetAsync(_anna, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), TestContext.Current.CancellationToken));
    }
}
