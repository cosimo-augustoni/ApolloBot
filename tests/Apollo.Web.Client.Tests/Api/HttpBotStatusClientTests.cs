using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpBotStatusClientTests
{
    [Fact]
    public async Task GetStatus_ShouldReadTheBotStatus()
    {
        var now = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);
        EventTypeCountDto[] types = [new(DataEventType.Message, 3), new(DataEventType.PresenceActivity, 1)];
        HourlyEventCountDto[] hours = [new(now.AddHours(-1), 4, types), new(now, 0, [])];
        OpenVoiceSessionDto[] voice = [new("Anna", Guid.NewGuid(), "Voice-Lounge", now.AddMinutes(-50)), new(null, Guid.NewGuid(), null, now.AddMinutes(-5))];
        var expected = new BotStatusDto(
            now,
            now.AddSeconds(-30),
            Connected: false,
            LastConnectedAt: now.AddMinutes(-5),
            GatewayLatencyMeanMs: 84,
            now.AddSeconds(-12),
            new LastRecordedEventDto(DataEventType.VoiceSession, now.AddSeconds(-12), "Voice-Lounge"),
            voice,
            IsStale: false,
            NoTrackingRole: true,
            hours);
        var handler = new StubHttpMessageHandler(expected);

        var status = await new HttpBotStatusClient(handler.CreateClient()).GetStatusAsync(TestContext.Current.CancellationToken);

        status.OpenVoiceSessions.ShouldBe(voice);
        status.EventsPerHour.Select(h => (h.HourStart, h.Count)).ShouldBe(hours.Select(h => (h.HourStart, h.Count)));
        status.EventsPerHour[0].Types.ShouldBe(types);
        (status with { OpenVoiceSessions = voice, EventsPerHour = hours }).ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/admin/bot-status");
    }
}
