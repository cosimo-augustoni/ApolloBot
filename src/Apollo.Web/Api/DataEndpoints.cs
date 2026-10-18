using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The data API of the admin page (#32, scope §7.2) under <c>/api/admin/data</c>, in the
/// <see cref="AdminEndpoints.MapAdminGroup"/> group: event counts per table and day, tracked vs. untracked members, the
/// raw event browser and the choices of its user and channel filters. Every event query is bounded in time; a range
/// longer than a year, an unknown type or a page larger than <see cref="RawEventQuery.MaxPageSize"/> is 400 with a
/// validation problem.
/// </summary>
public static class DataEndpoints
{
    public const string GroupPath = "/data";

    public static IEndpointRouteBuilder MapDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var data = endpoints.MapAdminGroup().MapGroup(GroupPath);

        data.MapGet("/counts", GetCountsAsync);
        data.MapGet("/members", GetMembersAsync);
        data.MapGet("/events", GetEventsAsync);
        data.MapGet("/users", SearchUsersAsync);
        data.MapGet("/channels", SearchChannelsAsync);

        return endpoints;
    }

    /// <summary><paramref name="from"/> and <paramref name="to"/> are <c>Europe/Zurich</c> days, both included; default: the last 30 days.</summary>
    private static async Task<Results<Ok<EventCountsDto>, ValidationProblem>> GetCountsAsync(
        DateOnly? from, DateOnly? to, DataOverviewQuery query, CancellationToken cancellationToken)
    {
        var range = query.ResolveCountsRange(from, to);
        if (range.From > range.To)
        {
            return Invalid(nameof(from), "The range must not end before it starts.");
        }

        if (range.To.DayNumber - range.From.DayNumber + 1 > DataOverviewQuery.MaxDays)
        {
            return Invalid(nameof(to), $"The range must not be longer than {DataOverviewQuery.MaxDays} days.");
        }

        return TypedResults.Ok(await query.GetCountsAsync(range.From, range.To, cancellationToken));
    }

    private static async Task<Ok<IReadOnlyList<MemberTrackingDto>>> GetMembersAsync(DataOverviewQuery query, CancellationToken cancellationToken) =>
        TypedResults.Ok(await query.GetMemberTrackingAsync(cancellationToken));

    /// <summary>
    /// One page of <paramref name="type"/> (default <c>message</c>); <paramref name="to"/> defaults to now and
    /// <paramref name="from"/> to <see cref="RawEventQuery.DefaultRange"/> before it.
    /// </summary>
    private static async Task<Results<Ok<RawEventPageDto>, ValidationProblem>> GetEventsAsync(
        string? type,
        Guid? userId,
        Guid? channelId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? page,
        int? pageSize,
        RawEventQuery query,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (ParseType(type) is not { } eventType)
        {
            return Invalid(nameof(type), $"The type must be one of: {string.Join(", ", Enum.GetNames<DataEventType>())}.");
        }

        var size = pageSize ?? RawEventQuery.DefaultPageSize;
        if (size is < 1 or > RawEventQuery.MaxPageSize)
        {
            return Invalid(nameof(pageSize), $"The page size must be between 1 and {RawEventQuery.MaxPageSize}.");
        }

        if (page is < 1)
        {
            return Invalid(nameof(page), "The page must be 1 or higher.");
        }

        // The query skips (page - 1) * pageSize rows as an int; a larger offset would overflow into a negative one.
        if (((long)(page ?? 1) - 1) * size > int.MaxValue)
        {
            return Invalid(nameof(page), "The page is too far back.");
        }

        var end = to ?? time.GetUtcNow();
        var start = from ?? end - RawEventQuery.DefaultRange;
        if (start > end)
        {
            return Invalid(nameof(from), "The range must not end before it starts.");
        }

        // Zurich wall-clock time, so 366 calendar days are allowed even when they gain an hour at the end of summer time.
        if (ZurichTime.WallClockSpan(start, end) > RawEventQuery.MaxRange)
        {
            return Invalid(nameof(to), $"The range must not be longer than {RawEventQuery.MaxRange.TotalDays} days.");
        }

        return TypedResults.Ok(await query.GetPageAsync(
            new RawEventSearch(eventType, userId, channelId, start, end, page ?? 1, size), cancellationToken));
    }

    private static async Task<Ok<IReadOnlyList<EventUserDto>>> SearchUsersAsync(string? search, RawEventQuery query, CancellationToken cancellationToken) =>
        TypedResults.Ok(await query.SearchUsersAsync(search, cancellationToken));

    private static async Task<Ok<IReadOnlyList<EventChannelDto>>> SearchChannelsAsync(string? search, RawEventQuery query, CancellationToken cancellationToken) =>
        TypedResults.Ok(await query.SearchChannelsAsync(search, cancellationToken));

    /// <summary>
    /// A <see cref="DataEventType"/> by name in any case (so its JSON name <c>voiceSession</c> too); missing means
    /// <see cref="DataEventType.Message"/>. Names only: <c>Enum.TryParse</c> would also take <c>0</c>.
    /// </summary>
    private static DataEventType? ParseType(string? type)
    {
        if (type is null)
        {
            return DataEventType.Message;
        }

        var name = Enum.GetNames<DataEventType>().FirstOrDefault(n => string.Equals(n, type, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Enum.Parse<DataEventType>(name);
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = [message] });
}
