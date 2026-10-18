using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>What the raw event browser (#32) asks for; the filters combine with AND.</summary>
/// <param name="Type">The table to browse.</param>
/// <param name="UserId">Only events of this user.</param>
/// <param name="ChannelId">Only events in this channel.</param>
/// <param name="From">The first instant; <see langword="null"/>: 24 hours before <paramref name="To"/>.</param>
/// <param name="To">The end, excluded; <see langword="null"/>: now.</param>
/// <param name="Page">The page, from 1.</param>
/// <param name="PageSize">Events per page, at most 200.</param>
public sealed record RawEventFilter(
    DataEventType Type,
    Guid? UserId = null,
    Guid? ChannelId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = RawEventFilter.DefaultPageSize)
{
    public const int DefaultPageSize = 50;
}
