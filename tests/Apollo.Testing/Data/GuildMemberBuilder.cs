using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Platform;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="GuildMember"/> row as the guild state sync (#13) stores it: by default a current member of
/// <see cref="TestPlatform.Guild"/> without the Administrator flag. <see cref="SeedAsync"/> resolves the guild and
/// user through <see cref="IPlatformIdResolver"/> and inserts the row.
/// </summary>
public sealed class GuildMemberBuilder : TestDataBuilder<GuildMemberBuilder, GuildMember>
{
    private PlatformRef _guild = TestPlatform.Guild;
    private PlatformRef _user = TestPlatform.User("alice");

    public GuildMemberBuilder For(PlatformRef user, PlatformRef? guild = null)
    {
        _user = user;
        _guild = guild ?? _guild;
        return this;
    }

    public GuildMemberBuilder AsAdministrator(bool isAdministrator = true) => With(m =>
    {
        m.IsAdministrator = isAdministrator;
        return m;
    });

    public GuildMemberBuilder LeftAt(DateTimeOffset leftAt) => With(m =>
    {
        m.LeftAt = leftAt;
        return m;
    });

    public async Task<GuildMember> SeedAsync(ApolloDbContext db, IPlatformIdResolver ids, CancellationToken cancellationToken)
    {
        var values = Build();
        var row = new GuildMember
        {
            GuildId = await ids.ResolveGuildAsync(_guild, cancellationToken),
            UserId = await ids.ResolveUserAsync(_user, cancellationToken),
            JoinedAt = values.JoinedAt,
            IsAdministrator = values.IsAdministrator,
            IsBot = values.IsBot,
            LeftAt = values.LeftAt,
        };
        db.GuildMembers.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    protected override GuildMember CreateDefault() => new() { JoinedAt = TestTime.Now.AddDays(-30) };
}
