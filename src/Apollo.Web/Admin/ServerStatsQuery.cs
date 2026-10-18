using Apollo.Data;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Stats.Activity;
using Contracts = Apollo.Web.Client.Contracts;

namespace Apollo.Web.Admin;

/// <summary>
/// The server dashboard's stats (#37, scope §7.2): <see cref="IActivityStatsService"/> for the configured guild over
/// Zurich days, bots left out, as the API contract.
/// </summary>
public sealed class ServerStatsQuery(ApolloDbContext db, IEnumerable<IPlatformIdentityProvider> providers, IActivityStatsService stats)
{
    /// <exception cref="ArgumentException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    public async Task<Contracts.ActivityStatsDto> GetAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // Only Discord has an identity provider until Fluxer (M7), which will need a choice of platform here. Before the
        // bot has seen the guild there is no ID: Guid.Empty matches no row, so the answer is the zero-filled range.
        var guildId = (await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken)).FirstOrDefault();
        var result = await stats.GetAsync(new ActivityStatsQuery(guildId, UserId: null, from, to), cancellationToken);
        return result.ToContract();
    }
}
