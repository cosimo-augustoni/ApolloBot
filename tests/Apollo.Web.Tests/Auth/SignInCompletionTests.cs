using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Sessions;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Web.Auth;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Tests.Auth;

public sealed class SignInCompletionTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _discordUser = new(PlatformKind.Discord, "80351110224678912");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompleteSignIn_WhenUserUnknown_ShouldCreateInternalUserAndMapping()
    {
        await using var db = Fixture.CreateDbContext();

        var principal = await CreateCompletion(db).CompleteAsync(new PlatformIdentity(_discordUser, "Nelly", null), Ct);

        var mapping = await db.UserPlatformIds.AsNoTracking().SingleAsync(Ct);
        mapping.Platform.ShouldBe(PlatformKind.Discord);
        mapping.ExternalId.ShouldBe(_discordUser.ExternalId);
        (await db.Users.AsNoTracking().Select(u => u.Id).ToListAsync(Ct)).ShouldBe([mapping.UserId]);
        ApolloPrincipal.ToCurrentUser(principal).ShouldBe(new(mapping.UserId, "Discord", "Nelly", null, IsAdmin: false));
    }

    [Fact]
    public async Task CompleteSignIn_WhenUserKnown_ShouldReuseInternalUserAndRefreshProfileInSession()
    {
        Guid existing;
        await using (var seed = Fixture.CreateDbContext())
        {
            var first = await CreateCompletion(seed).CompleteAsync(new PlatformIdentity(_discordUser, "Nelly", null), Ct);
            existing = ApolloPrincipal.ToCurrentUser(first).UserId;
        }

        await using var db = Fixture.CreateDbContext();
        var principal = await CreateCompletion(db).CompleteAsync(
            new PlatformIdentity(_discordUser, "Nelly Renamed", "https://cdn.discordapp.com/avatars/80351110224678912/abc.png"), Ct);

        (await db.Users.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
        (await db.UserPlatformIds.AsNoTracking().CountAsync(Ct)).ShouldBe(1);
        ApolloPrincipal.ToCurrentUser(principal).ShouldBe(new(existing, "Discord", "Nelly Renamed", "https://cdn.discordapp.com/avatars/80351110224678912/abc.png"));
    }

    [Fact]
    public async Task CompleteSignIn_WhenUserIsAdministratorInTheConfiguredGuild_ShouldIssueAdminClaim()
    {
        await using var db = Fixture.CreateDbContext();
        await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).AsAdministrator().SeedAsync(db, Ids(db), Ct);

        var principal = await CreateCompletion(db).CompleteAsync(new PlatformIdentity(_discordUser, "Nelly", null), Ct);

        principal.HasClaim(ApolloClaimTypes.Admin, "true").ShouldBeTrue();
        ApolloPrincipal.ReadSession(principal).ShouldBe(new SessionClaims(
            ApolloPrincipal.ToCurrentUser(principal).UserId, PlatformKind.Discord, TestTime.Now, TestTime.Now));
    }

    [Fact]
    public async Task CompleteSignIn_WhenUserIsMemberWithoutAdministrator_ShouldIssueNoAdminClaim()
    {
        await using var db = Fixture.CreateDbContext();
        await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).SeedAsync(db, Ids(db), Ct);

        var principal = await CreateCompletion(db).CompleteAsync(new PlatformIdentity(_discordUser, "Nelly", null), Ct);

        principal.HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
    }

    [Fact]
    public async Task CompleteSignIn_WhenUserLoggedOutBefore_ShouldStartAValidSession()
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).AsAdministrator().SeedAsync(db, Ids(db), Ct);
        await new UserSignOuts(db).RecordAsync(member.UserId, TestTime.Now.AddSeconds(-1), Ct);

        var principal = await CreateCompletion(db).CompleteAsync(new PlatformIdentity(_discordUser, "Nelly", null), Ct);

        principal.HasClaim(ApolloClaimTypes.Admin, "true").ShouldBeTrue();
    }

    // A fresh cache per completion, as a new web process would have: Respawn deletes rows the cache would remember.
    private static SignInCompletion CreateCompletion(ApolloDbContext db)
    {
        var time = TestTime.CreateProvider();
        var revalidator = new AccessRevalidator(
            new GuildAccessReader(db), new UserSignOuts(db), [new FakeIdentityProvider()], Options.Create(new WebAuthOptions()), time);
        return new SignInCompletion(Ids(db), revalidator, time);
    }

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
