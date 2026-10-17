namespace Apollo.Web.Client.Contracts;

/// <summary><c>GET /api/admin/data/counts</c> (#32): the rows of each raw event table per <c>Europe/Zurich</c> day.</summary>
/// <param name="From">The first day of the range (Zurich calendar day).</param>
/// <param name="To">The last day of the range, included.</param>
/// <param name="Tables">One entry per <see cref="DataEventType"/>, in its order.</param>
public sealed record EventCountsDto(DateOnly From, DateOnly To, IReadOnlyList<EventTableCountDto> Tables);
