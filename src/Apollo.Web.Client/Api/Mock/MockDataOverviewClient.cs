using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Formatting;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IDataOverviewClient"/> without a host, for the prototype and bUnit tests: a guild with a tracking role,
/// 30 days of sample counts and a few sample events per type. Tests replace <see cref="Counts"/>,
/// <see cref="MemberTracking"/> or <see cref="Events"/> (or set <see cref="Failure"/>) and read <see cref="EventRequests"/>.
/// </summary>
public sealed class MockDataOverviewClient : IDataOverviewClient
{
    public static readonly EventUserDto Anna = new(Guid.Parse("0c7c4f63-7a4e-4b8e-9d64-0d1a2b3c4d01"), "Anna");
    public static readonly EventUserDto Ben = new(Guid.Parse("0c7c4f63-7a4e-4b8e-9d64-0d1a2b3c4d02"), "Ben");
    public static readonly EventUserDto Chris = new(Guid.Parse("0c7c4f63-7a4e-4b8e-9d64-0d1a2b3c4d03"), "Chris");
    public static readonly EventChannelDto Allgemein = new(Guid.Parse("5f1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e01"), "allgemein");
    public static readonly EventChannelDto Memes = new(Guid.Parse("5f1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e02"), "memes");
    public static readonly EventChannelDto Gaming = new(Guid.Parse("5f1d2e3c-4b5a-4c6d-8e7f-1a2b3c4d5e03"), "Gaming");

    private static readonly EventUserDto[] _users = [Anna, Ben, Chris];
    private static readonly EventChannelDto[] _channels = [Allgemein, Memes, Gaming];

    public MockDataOverviewClient(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, UiTime.Zone).DateTime);
        var from = today.AddDays(-29);
        Counts = new EventCountsDto(from, today, [.. Enum.GetValues<DataEventType>().Select(type =>
        {
            var days = Enumerable.Range(0, 30).Select(i => new DailyEventCountDto(from.AddDays(i), SampleCount(type, i))).ToList();
            return new EventTableCountDto(type, days.Sum(d => d.Count), days);
        })]);
        MemberTracking = [new MemberTrackingDto("Discord", 168, 46, 214, RoleSettingsMode.Active)];
        Events = filter => SamplePage(filter, now);
    }

    /// <summary>What the next counts load returns.</summary>
    public EventCountsDto Counts { get; set; }

    /// <summary>What the next member load returns.</summary>
    public IReadOnlyList<MemberTrackingDto> MemberTracking { get; set; }

    /// <summary>Answers an event request.</summary>
    public Func<RawEventFilter, RawEventPageDto> Events { get; set; }

    /// <summary>When set, every load throws it instead.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The event requests, in order.</summary>
    public List<RawEventFilter> EventRequests { get; } = [];

    public Task<EventCountsDto> GetCountsAsync(CancellationToken cancellationToken) => Answer(Counts);

    public Task<IReadOnlyList<MemberTrackingDto>> GetMemberTrackingAsync(CancellationToken cancellationToken) => Answer(MemberTracking);

    public Task<RawEventPageDto> GetEventsAsync(RawEventFilter filter, CancellationToken cancellationToken)
    {
        EventRequests.Add(filter);
        return Failure is null ? Task.FromResult(Events(filter)) : Task.FromException<RawEventPageDto>(Failure);
    }

    public Task<IReadOnlyList<EventUserDto>> SearchUsersAsync(string search, CancellationToken cancellationToken) =>
        Answer<IReadOnlyList<EventUserDto>>([.. _users.Where(u => u.DisplayName!.Contains(search ?? string.Empty, StringComparison.OrdinalIgnoreCase))]);

    public Task<IReadOnlyList<EventChannelDto>> SearchChannelsAsync(string search, CancellationToken cancellationToken) =>
        Answer<IReadOnlyList<EventChannelDto>>([.. _channels.Where(c => c.Name!.Contains(search ?? string.Empty, StringComparison.OrdinalIgnoreCase))]);

    private Task<T> Answer<T>(T value) => Failure is { } failure ? Task.FromException<T>(failure) : Task.FromResult(value);

    // Deterministic, a little busier towards today; messages and reactions dominate as on a real server.
    private static int SampleCount(DataEventType type, int day) => type switch
    {
        DataEventType.Message => 900 + (day * 12) + (day % 7 * 40),
        DataEventType.VoiceSession => 30 + (day % 5 * 4),
        DataEventType.VoiceStateSegment => 100 + (day % 5 * 9),
        DataEventType.Reaction => 450 + (day * 5),
        DataEventType.Membership => day % 9 == 0 ? 1 : 0,
        _ => 30 + (day % 3 * 5),
    };

    private static RawEventPageDto SamplePage(RawEventFilter filter, DateTimeOffset now)
    {
        RawEventDto[] all = filter.Type switch
        {
            DataEventType.Message =>
            [
                new(4, DataEventType.Message, now.AddMinutes(-3), Anna, Allgemein)
                {
                    Message = new("Create", 42, 8, 1, 0, 0, 0, 0, false, ["Text", "Attachment"], null, null, false),
                },
                new(3, DataEventType.Message, now.AddMinutes(-35), Ben, Memes)
                {
                    Message = new("Create", 12, 3, 0, 0, 1, 1, 0, false, ["Text", "Mention", "Emoji"], null, Anna, false),
                },
                new(2, DataEventType.Message, now.AddHours(-2), Chris, Allgemein)
                {
                    Message = new("Delete", null, null, null, null, null, null, null, null, null, null, null, false),
                },
            ],
            DataEventType.VoiceSession =>
            [
                new(2, DataEventType.VoiceSession, now.AddMinutes(-50), Chris, Gaming) { VoiceSession = new(null, null) },
                new(1, DataEventType.VoiceSession, now.AddHours(-3), Anna, Gaming) { VoiceSession = new(now.AddHours(-2), "Left") },
            ],
            DataEventType.VoiceStateSegment =>
            [
                new(5, DataEventType.VoiceStateSegment, now.AddMinutes(-20), Chris, null) { VoiceSegment = new(2, null, true, false, true, false) },
                new(4, DataEventType.VoiceStateSegment, now.AddMinutes(-50), Chris, null) { VoiceSegment = new(2, now.AddMinutes(-20), false, false, false, false) },
            ],
            DataEventType.Reaction =>
            [
                new(9, DataEventType.Reaction, now.AddMinutes(-4), Ben, Memes) { Reaction = new("Add", "lachen", true, Anna) },
                new(8, DataEventType.Reaction, now.AddMinutes(-40), Anna, Allgemein) { Reaction = new("Add", "👍", false, null) },
            ],
            DataEventType.Membership =>
            [
                new(1, DataEventType.Membership, now.AddHours(-5), Chris, null) { Membership = new("Join") },
            ],
            _ =>
            [
                new(3, DataEventType.PresenceActivity, now.AddHours(-1), Anna, null) { PresenceActivity = new("Playing", "Factorio", now.AddMinutes(-10)) },
            ],
        };

        var matching = all
            .Where(e => filter.UserId is not { } user || e.User.Id == user)
            .Where(e => filter.ChannelId is not { } channel || e.Channel?.Id == channel)
            .ToList();
        return new RawEventPageDto(matching.Count, [.. matching.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)]);
    }
}
