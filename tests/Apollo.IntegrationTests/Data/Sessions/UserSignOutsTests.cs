using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Sessions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Sessions;

public sealed class UserSignOutsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetLast_WhenUserNeverSignedOut_ShouldReturnNull()
    {
        await using var db = Fixture.CreateDbContext();
        var user = await new UserBuilder().SeedAsync(db, Ids(db), Ct);

        (await new UserSignOuts(db).GetLastAsync(user.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Record_WhenUserSignsOutAgain_ShouldKeepTheLatestTime()
    {
        await using var db = Fixture.CreateDbContext();
        var user = await new UserBuilder().SeedAsync(db, Ids(db), Ct);
        var signOuts = new UserSignOuts(db);

        await signOuts.RecordAsync(user.Id, TestTime.Now, Ct);
        await signOuts.RecordAsync(user.Id, TestTime.Now.AddHours(1), Ct);

        (await signOuts.GetLastAsync(user.Id, Ct)).ShouldBe(TestTime.Now.AddHours(1));
    }

    [Fact]
    public async Task Record_WhenAnEarlierTimeArrivesLate_ShouldKeepTheLatestTime()
    {
        await using var db = Fixture.CreateDbContext();
        var user = await new UserBuilder().For(TestPlatform.User("bob")).SeedAsync(db, Ids(db), Ct);
        var signOuts = new UserSignOuts(db);

        await signOuts.RecordAsync(user.Id, TestTime.Now.AddHours(1), Ct);
        await signOuts.RecordAsync(user.Id, TestTime.Now, Ct);

        (await signOuts.GetLastAsync(user.Id, Ct)).ShouldBe(TestTime.Now.AddHours(1));
    }

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
