using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Testing.Data;
using Apollo.Web.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

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
        ApolloPrincipal.ToCurrentUser(principal).ShouldBe(new(mapping.UserId, "Discord", "Nelly"));
    }

    [Fact]
    public async Task CompleteSignIn_WhenUserKnown_ShouldReuseInternalUserAndUpdateProfile()
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
        ApolloPrincipal.ToCurrentUser(principal).ShouldBe(new(existing, "Discord", "Nelly Renamed"));
    }

    // A fresh cache per completion, as a new web process would have: Respawn deletes rows the cache would remember.
    private static SignInCompletion CreateCompletion(ApolloDbContext db) =>
        new(new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance));
}
