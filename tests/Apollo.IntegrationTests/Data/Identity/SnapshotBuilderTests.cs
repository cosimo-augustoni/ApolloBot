using Apollo.Data.Identity;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Identity;

/// <summary>
/// The <c>Apollo.Testing.Data</c> builders seed internal IDs through the resolver and store the snapshot columns (#13).
/// </summary>
public sealed class SnapshotBuilderTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeedAsync_WhenBuildersAreSeeded_ShouldStoreSnapshotColumnsUnderTheResolvedIds()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);

        var owner = await new UserBuilder().For(TestPlatform.User("owner")).Named("Owner").WithAvatar("https://cdn.example.test/o.png").SeedAsync(db, ids, Ct);
        var guild = await new GuildBuilder().Named("Apollo").OwnedBy(owner.Id).SeedAsync(db, ids, Ct);
        var role = await new RoleBuilder().For(TestPlatform.Role("admin")).Named("Admin").AtPosition(3).AsAdministrator().WithColor(0xABCDEF).SeedAsync(db, ids, Ct);
        var channel = await new ChannelBuilder().For(TestPlatform.Channel("lounge")).Named("Lounge").OfType(PlatformChannelType.Voice).DeletedAt(TestTime.Now).SeedAsync(db, ids, Ct);

        await using var read = Fixture.CreateDbContext();
        (await read.Users.SingleAsync(u => u.Id == owner.Id, Ct)).DisplayName.ShouldBe("Owner");
        (await read.Guilds.SingleAsync(g => g.Id == guild.Id, Ct)).OwnerUserId.ShouldBe(owner.Id);
        var storedRole = await read.Roles.SingleAsync(r => r.Id == role.Id, Ct);
        storedRole.GuildId.ShouldBe(guild.Id);
        storedRole.Name.ShouldBe("Admin");
        storedRole.Position.ShouldBe(3);
        storedRole.IsAdministrator.ShouldBeTrue();
        storedRole.Color.ShouldBe(0xABCDEF);
        var storedChannel = await read.Channels.SingleAsync(c => c.Id == channel.Id, Ct);
        storedChannel.Type.ShouldBe(PlatformChannelType.Voice);
        storedChannel.DeletedAt.ShouldBe(TestTime.Now);
        (await ids.TryGetRoleAsync(TestPlatform.Role("admin"), Ct)).ShouldBe(role.Id);
    }
}
