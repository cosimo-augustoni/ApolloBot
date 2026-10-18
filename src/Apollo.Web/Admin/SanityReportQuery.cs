using Apollo.Data;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Stats.Sanity;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Admin;

/// <summary>
/// The data sanity page (#40, scope §7.2): <see cref="IDataSanityService"/> for the configured guild over Zurich days,
/// with the users of the examples named. Heartbeat gaps use the stale threshold of the bot status page
/// (<see cref="BotStatusOptions.StaleAfter"/>), so a gap here is a time that page would have warned about.
/// </summary>
public sealed class SanityReportQuery(
    ApolloDbContext db,
    IEnumerable<IPlatformIdentityProvider> providers,
    IDataSanityService sanity,
    IOptions<BotStatusOptions> options)
{
    /// <exception cref="ArgumentException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    public async Task<DataSanityDto> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // Only Discord has an identity provider until Fluxer (M7), as for the dashboard. Before the bot has seen the
        // guild there is no ID: Guid.Empty matches no row, so every check is 0.
        var guildId = (await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken)).FirstOrDefault();
        var threshold = options.Value.StaleAfter;
        var report = await sanity.GetAsync(new DataSanityQuery(guildId, from, to, threshold), cancellationToken);

        Guid[] userIds =
        [
            .. report.LongSessions.Examples.Select(s => s.UserId),
            .. report.OpenSessions.Examples.Select(s => s.UserId),
            .. report.UntrackedEvents.Examples.Select(r => r.UserId),
            .. report.UntrackedReferences.Examples.Select(r => r.UserId),
        ];
        var names = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
        EventUserDto User(Guid id) => new(id, names.GetValueOrDefault(id));
        SanityCheckDto<SanitySessionDto> Sessions(SanityCheck<SanitySession> check) =>
            new(check.Count, [.. check.Examples.Select(s => new SanitySessionDto(s.Table, s.Id, User(s.UserId), s.StartedAt, s.EndedAt))]);
        SanityCheckDto<UntrackedRowDto> Rows(SanityCheck<UntrackedRow> check) =>
            new(check.Count, [.. check.Examples.Select(r => new UntrackedRowDto(r.Table, r.Column, r.Id, User(r.UserId), r.OccurredAt))]);

        return new DataSanityDto(
            from,
            to,
            (int)threshold.TotalSeconds,
            new SanityCheckDto<HeartbeatGapDto>(report.HeartbeatGaps.Count, [.. report.HeartbeatGaps.Examples.Select(g => new HeartbeatGapDto(g.Start, g.End))]),
            Sessions(report.LongSessions),
            Sessions(report.OpenSessions),
            Rows(report.UntrackedEvents),
            Rows(report.UntrackedReferences));
    }
}
