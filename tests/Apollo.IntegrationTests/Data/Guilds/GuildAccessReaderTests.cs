using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Guilds;

public sealed class GuildAccessReaderTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _otherGuild = new(PlatformKind.Discord, "other-guild");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WhenCurrentMemberHasAdministratorFlag_ShouldReturnAdministrator()
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder().For(_alice).AsAdministrator().SeedAsync(db, Ids(db), Ct);

        var access = await new GuildAccessReader(db).GetAsync(member.UserId, TestPlatform.Guild, Ct);

        access.ShouldBe(GuildAccess.Administrator);
    }

    [Fact]
    public async Task Get_WhenCurrentMemberHasNoAdministratorFlag_ShouldReturnMember()
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder().For(_alice).SeedAsync(db, Ids(db), Ct);

        var access = await new GuildAccessReader(db).GetAsync(member.UserId, TestPlatform.Guild, Ct);

        access.ShouldBe(GuildAccess.Member);
    }

    [Fact]
    public async Task Get_WhenAdministratorLeftTheGuild_ShouldReturnNotMember()
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder().For(_alice).AsAdministrator().LeftAt(TestTime.Now).SeedAsync(db, Ids(db), Ct);

        var access = await new GuildAccessReader(db).GetAsync(member.UserId, TestPlatform.Guild, Ct);

        access.ShouldBe(GuildAccess.NotMember);
    }

    [Fact]
    public async Task Get_WhenAdministratorOfAnotherGuildOnly_ShouldReturnNotMember()
    {
        await using var db = Fixture.CreateDbContext();
        await new GuildBuilder().SeedAsync(db, Ids(db), Ct);
        var member = await new GuildMemberBuilder().For(_alice, _otherGuild).AsAdministrator().SeedAsync(db, Ids(db), Ct);

        var access = await new GuildAccessReader(db).GetAsync(member.UserId, TestPlatform.Guild, Ct);

        access.ShouldBe(GuildAccess.NotMember);
    }

    [Fact]
    public async Task Get_WhenGuildIsOnAnotherPlatform_ShouldReturnNotMember()
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder().For(_alice).AsAdministrator().SeedAsync(db, Ids(db), Ct);

        var access = await new GuildAccessReader(db)
            .GetAsync(member.UserId, new PlatformRef(PlatformKind.Fluxer, TestPlatform.Guild.ExternalId), Ct);

        access.ShouldBe(GuildAccess.NotMember);
    }

    [Fact]
    public async Task Get_WhenGuildNeverSynced_ShouldReturnNotMember()
    {
        await using var db = Fixture.CreateDbContext();
        var user = await new UserBuilder().For(_alice).SeedAsync(db, Ids(db), Ct);

        var access = await new GuildAccessReader(db).GetAsync(user.Id, TestPlatform.Guild, Ct);

        access.ShouldBe(GuildAccess.NotMember);
    }

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
