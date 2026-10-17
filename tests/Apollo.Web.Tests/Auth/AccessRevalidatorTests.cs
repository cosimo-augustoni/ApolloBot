using System.Data.Common;
using System.Security.Claims;
using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Sessions;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Web.Auth;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Auth;

public sealed class AccessRevalidatorTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _nelly = new(PlatformKind.Discord, "80351110224678912");
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AccessRevalidator_WhenIntervalNotElapsed_ShouldNotQueryDatabase()
    {
        var userId = await SeedMemberAsync(isAdministrator: false);
        var principal = Session(userId, isAdministrator: true, checkedAt: _time.GetUtcNow());
        _time.Advance(_interval - TimeSpan.FromSeconds(1));
        var commands = new CommandCounter();
        await using var db = new ApolloDbContext(
            new DbContextOptionsBuilder<ApolloDbContext>(ApolloDbContext.CreateOptions(Fixture.ConnectionString)).AddInterceptors(commands).Options);

        var result = await CreateRevalidator(db).RevalidateAsync(principal, Ct);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Unchanged);
        commands.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AccessRevalidator_WhenIntervalElapsed_ShouldQueryDatabase()
    {
        var userId = await SeedMemberAsync(isAdministrator: false);
        var principal = Session(userId, isAdministrator: true, checkedAt: _time.GetUtcNow());
        _time.Advance(_interval);
        var commands = new CommandCounter();
        await using var db = new ApolloDbContext(
            new DbContextOptionsBuilder<ApolloDbContext>(ApolloDbContext.CreateOptions(Fixture.ConnectionString)).AddInterceptors(commands).Options);

        await CreateRevalidator(db).RevalidateAsync(principal, Ct);

        commands.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task AccessRevalidator_WhenAdminFlagRemoved_ShouldDropAdminClaim()
    {
        var userId = await SeedMemberAsync(isAdministrator: false);
        var principal = Session(userId, isAdministrator: true, checkedAt: _time.GetUtcNow());
        _time.Advance(_interval);

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Refreshed);
        result.Principal.ShouldNotBeNull().HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
    }

    [Fact]
    public async Task AccessRevalidator_WhenAdminFlagAdded_ShouldAddAdminClaim()
    {
        var userId = await SeedMemberAsync(isAdministrator: true);
        var principal = Session(userId, isAdministrator: false, checkedAt: _time.GetUtcNow());
        _time.Advance(_interval);

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Refreshed);
        result.Principal.ShouldNotBeNull().HasClaim(ApolloClaimTypes.Admin, "true").ShouldBeTrue();
    }

    [Fact]
    public async Task AccessRevalidator_WhenNotGuildMember_ShouldDropAdminClaim()
    {
        Guid userId;
        await using (var db = Fixture.CreateDbContext())
        {
            userId = (await new UserBuilder().For(_nelly).SeedAsync(db, Ids(db), Ct)).Id;
        }

        var principal = Session(userId, isAdministrator: true, checkedAt: _time.GetUtcNow());
        _time.Advance(_interval);

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Refreshed);
        result.Principal.ShouldNotBeNull().HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
        result.Principal.Identity!.IsAuthenticated.ShouldBeTrue();
    }

    [Fact]
    public async Task AccessRevalidator_WhenAdministratorOfAnotherGuildOnly_ShouldNotAddAdminClaim()
    {
        var userId = await SeedMemberAsync(isAdministrator: true, guild: new PlatformRef(PlatformKind.Discord, "333333333333333333"));
        var principal = Session(userId, isAdministrator: false, checkedAt: null);

        var result = await RevalidateAsync(principal);

        result.Principal.ShouldNotBeNull().HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
    }

    [Fact]
    public async Task AccessRevalidator_WhenNeverChecked_ShouldCheckAndStampTheCheckTime()
    {
        var userId = await SeedMemberAsync(isAdministrator: true);
        var principal = Session(userId, isAdministrator: false, checkedAt: null);

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Refreshed);
        result.Principal.ShouldNotBeNull().HasClaim(ApolloClaimTypes.Admin, "true").ShouldBeTrue();
        ApolloPrincipal.ReadSession(result.Principal).ShouldNotBeNull().AccessCheckedAt.ShouldBe(_time.GetUtcNow());
    }

    [Fact]
    public async Task AccessRevalidator_WhenRefreshed_ShouldKeepTheSessionClaims()
    {
        var userId = await SeedMemberAsync(isAdministrator: true);
        var principal = Session(userId, isAdministrator: false, checkedAt: null);

        var result = await RevalidateAsync(principal);

        ApolloPrincipal.ToCurrentUser(result.Principal!).ShouldBe(new CurrentUserDto(userId, "Discord", "Nelly", null, IsAdmin: true));
        ApolloPrincipal.ReadSession(result.Principal!).ShouldNotBeNull().SignedInAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task AccessRevalidator_WhenClockWentBackBeforeTheLastCheck_ShouldCheckAgain()
    {
        var userId = await SeedMemberAsync(isAdministrator: false);
        var principal = Session(userId, isAdministrator: true, checkedAt: _time.GetUtcNow().AddHours(1));

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Refreshed);
        result.Principal.ShouldNotBeNull().HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
    }

    [Fact]
    public async Task AccessRevalidator_WhenPlatformHasNoLogin_ShouldNotAddAdminClaim()
    {
        var userId = await SeedMemberAsync(isAdministrator: true);
        var principal = ApolloPrincipal.Create(userId, PlatformKind.Fluxer, "Nelly", null, TestTime.Now);

        var result = await RevalidateAsync(principal);

        result.Principal.ShouldNotBeNull().HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
    }

    [Fact]
    public async Task AccessRevalidator_WhenUserSignedOutAfterThisSessionStarted_ShouldReject()
    {
        var userId = await SeedMemberAsync(isAdministrator: true);
        var principal = Session(userId, isAdministrator: true, checkedAt: _time.GetUtcNow());
        _time.Advance(TimeSpan.FromMinutes(1));
        await RecordSignOutAsync(userId, _time.GetUtcNow());
        _time.Advance(_interval);

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Rejected);
        result.Principal.ShouldBeNull();
    }

    [Fact]
    public async Task AccessRevalidator_WhenUserSignedOutBeforeThisSessionStarted_ShouldRefresh()
    {
        var userId = await SeedMemberAsync(isAdministrator: true);
        await RecordSignOutAsync(userId, TestTime.Now.AddMinutes(-1));
        var principal = Session(userId, isAdministrator: false, checkedAt: null);

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Refreshed);
        result.Principal.ShouldNotBeNull().HasClaim(ApolloClaimTypes.Admin, "true").ShouldBeTrue();
    }

    [Fact]
    public async Task AccessRevalidator_WhenPrincipalIsNoSession_ShouldReject()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ApolloClaimTypes.Admin, "true")], AuthSchemes.Session));

        var result = await RevalidateAsync(principal);

        result.Outcome.ShouldBe(AccessRevalidationOutcome.Rejected);
    }

    private ClaimsPrincipal Session(Guid userId, bool isAdministrator, DateTimeOffset? checkedAt)
    {
        var principal = ApolloPrincipal.Create(userId, PlatformKind.Discord, "Nelly", null, TestTime.Now);
        return checkedAt is { } at ? ApolloPrincipal.WithAccess(principal, isAdministrator, at) : principal;
    }

    private async Task<Guid> SeedMemberAsync(bool isAdministrator, PlatformRef? guild = null)
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder()
            .For(_nelly, guild ?? ApolloWebApplicationFactory.Guild)
            .AsAdministrator(isAdministrator)
            .SeedAsync(db, Ids(db), Ct);
        return member.UserId;
    }

    private async Task RecordSignOutAsync(Guid userId, DateTimeOffset at)
    {
        await using var db = Fixture.CreateDbContext();
        await new UserSignOuts(db).RecordAsync(userId, at, Ct);
    }

    private async Task<AccessRevalidation> RevalidateAsync(ClaimsPrincipal principal)
    {
        await using var db = Fixture.CreateDbContext();
        return await CreateRevalidator(db).RevalidateAsync(principal, Ct);
    }

    private AccessRevalidator CreateRevalidator(ApolloDbContext db) =>
        new(new GuildAccessReader(db), new UserSignOuts(db), [new FakeIdentityProvider()],
            Options.Create(new WebAuthOptions { RevalidationInterval = _interval }), _time);

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
