using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>The admin data page (#32, <c>/api/admin/data</c>).</summary>
public interface IDataOverviewClient
{
    /// <summary>The rows per raw event table and day of the last 30 <c>Europe/Zurich</c> days, today included.</summary>
    Task<EventCountsDto> GetCountsAsync(CancellationToken cancellationToken);

    /// <summary>Tracked and untracked members, one entry per platform with a synced guild.</summary>
    Task<IReadOnlyList<MemberTrackingDto>> GetMemberTrackingAsync(CancellationToken cancellationToken);

    Task<RawEventPageDto> GetEventsAsync(RawEventFilter filter, CancellationToken cancellationToken);

    /// <summary>Members whose name contains <paramref name="search"/>, for the user filter.</summary>
    Task<IReadOnlyList<EventUserDto>> SearchUsersAsync(string search, CancellationToken cancellationToken);

    /// <summary>Channels whose name contains <paramref name="search"/>, for the channel filter.</summary>
    Task<IReadOnlyList<EventChannelDto>> SearchChannelsAsync(string search, CancellationToken cancellationToken);
}
