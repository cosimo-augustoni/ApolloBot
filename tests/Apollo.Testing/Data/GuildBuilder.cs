using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="Guild"/> row with its snapshot columns. <see cref="SeedAsync"/> creates the internal ID through
/// <see cref="IPlatformIdResolver"/> (the guild defaults to <see cref="TestPlatform.Guild"/>) and stores the columns.
/// </summary>
public sealed class GuildBuilder : TestDataBuilder<GuildBuilder, Guild>
{
    private PlatformRef _guild = TestPlatform.Guild;

    public GuildBuilder For(PlatformRef guild)
    {
        _guild = guild;
        return this;
    }

    public GuildBuilder Named(string name) => With(g =>
    {
        g.Name = name;
        return g;
    });

    public GuildBuilder OwnedBy(Guid ownerUserId) => With(g =>
    {
        g.OwnerUserId = ownerUserId;
        return g;
    });

    public async Task<Guild> SeedAsync(ApolloDbContext db, IPlatformIdResolver ids, CancellationToken cancellationToken)
    {
        var id = await ids.ResolveGuildAsync(_guild, cancellationToken);
        var row = await db.Guilds.SingleAsync(g => g.Id == id, cancellationToken);
        var values = Build();
        row.Name = values.Name;
        row.OwnerUserId = values.OwnerUserId;
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    protected override Guild CreateDefault() => new() { Name = "Apollo Test Guild" };
}
