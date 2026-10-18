using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpDataOverviewClientTests
{
    private static readonly Guid _alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _general = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetCounts_ShouldReadTheCounts()
    {
        var expected = new EventCountsDto(new DateOnly(2026, 1, 14), new DateOnly(2026, 1, 15), []);
        var handler = new StubHttpMessageHandler(expected);

        var counts = await new HttpDataOverviewClient(handler.CreateClient()).GetCountsAsync(Ct);

        counts.From.ShouldBe(expected.From);
        counts.To.ShouldBe(expected.To);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/data/counts");
    }

    [Fact]
    public async Task GetMemberTracking_ShouldReadEveryPlatform()
    {
        MemberTrackingDto[] expected = [new("Discord", 168, 46, 214, RoleSettingsMode.Active)];
        var handler = new StubHttpMessageHandler(expected);

        var tracking = await new HttpDataOverviewClient(handler.CreateClient()).GetMemberTrackingAsync(Ct);

        tracking.ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/data/members");
    }

    [Fact]
    public async Task GetEvents_ShouldSendEveryFilterAsQuery()
    {
        var handler = new StubHttpMessageHandler(new RawEventPageDto(0, []));
        var from = new DateTimeOffset(2026, 1, 13, 23, 0, 0, TimeSpan.Zero);
        var filter = new RawEventFilter(DataEventType.VoiceSession, _alice, _general, from, from.AddDays(2), Page: 3, PageSize: 50);

        await new HttpDataOverviewClient(handler.CreateClient()).GetEventsAsync(filter, Ct);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.AbsolutePath.ShouldBe("/api/admin/data/events");
        Uri.UnescapeDataString(request.Query).ShouldBe(
            $"?type=voiceSession&page=3&pageSize=50&userId={_alice}&channelId={_general}&from=2026-01-13T23:00:00.0000000+00:00&to=2026-01-15T23:00:00.0000000+00:00");
    }

    [Fact]
    public async Task GetEvents_WhenNoOptionalFilter_ShouldSendTypeAndPageOnly()
    {
        var expected = new RawEventPageDto(1, [new RawEventDto(7, DataEventType.Message, DateTimeOffset.UnixEpoch, new EventUserDto(_alice, "Alice"), null)]);
        var handler = new StubHttpMessageHandler(expected);

        var page = await new HttpDataOverviewClient(handler.CreateClient()).GetEventsAsync(new RawEventFilter(DataEventType.Message), Ct);

        page.Items.ShouldHaveSingleItem().User.DisplayName.ShouldBe("Alice");
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/data/events?type=message&page=1&pageSize=50");
    }

    [Fact]
    public async Task SearchUsers_ShouldEscapeTheSearch()
    {
        EventUserDto[] expected = [new(_alice, "Alice")];
        var handler = new StubHttpMessageHandler(expected);

        var users = await new HttpDataOverviewClient(handler.CreateClient()).SearchUsersAsync("a&b", Ct);

        users.ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/data/users?search=a%26b");
    }

    [Fact]
    public async Task SearchChannels_ShouldEscapeTheSearch()
    {
        EventChannelDto[] expected = [new(_general, "allgemein")];
        var handler = new StubHttpMessageHandler(expected);

        var channels = await new HttpDataOverviewClient(handler.CreateClient()).SearchChannelsAsync("#all", Ct);

        channels.ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/data/channels?search=%23all");
    }
}
