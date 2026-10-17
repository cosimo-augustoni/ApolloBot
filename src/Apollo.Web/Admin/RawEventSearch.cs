using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Admin;

/// <summary>A validated filter of the raw event browser (#32); the filters combine with AND.</summary>
/// <param name="Type">The table to browse.</param>
/// <param name="UserId">Only rows of this internal user.</param>
/// <param name="ChannelId">Only rows in this internal channel; tables without a channel then have no rows.</param>
/// <param name="From">The first instant, included.</param>
/// <param name="To">The end, excluded.</param>
/// <param name="Page">The page, from 1.</param>
/// <param name="PageSize">Rows per page.</param>
public sealed record RawEventSearch(
    DataEventType Type,
    Guid? UserId,
    Guid? ChannelId,
    DateTimeOffset From,
    DateTimeOffset To,
    int Page,
    int PageSize);
