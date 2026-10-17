namespace Apollo.Web.Client.Contracts;

/// <summary><c>GET /api/admin/data/events</c> (#32): one page of raw events, newest first.</summary>
/// <param name="Total">All events matching the filter, on every page.</param>
/// <param name="Items">The events of the requested page.</param>
public sealed record RawEventPageDto(int Total, IReadOnlyList<RawEventDto> Items);
