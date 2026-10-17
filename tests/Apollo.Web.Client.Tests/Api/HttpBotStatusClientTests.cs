using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpBotStatusClientTests
{
    [Fact]
    public async Task GetStatus_ShouldReadTheBotStatus()
    {
        var now = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        HourlyEventCountDto[] hours = [new(now.AddHours(-1), 4), new(now, 2)];
        var expected = new BotStatusDto(now, now.AddSeconds(-30), true, 84, now.AddSeconds(-12), 3, IsStale: false, hours);
        var handler = new StubHttpMessageHandler(expected);

        var status = await new HttpBotStatusClient(handler.CreateClient()).GetStatusAsync(TestContext.Current.CancellationToken);

        status.EventsPerHour.ShouldBe(hours);
        (status with { EventsPerHour = hours }).ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/admin/bot-status");
    }
}
