using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.IntegrationTests.Data;

/// <summary>
/// The context as <see cref="ApolloDataExtensions.AddApolloDbContext{TBuilder}"/> registers it at runtime, on the
/// Testcontainers database (#77).
/// </summary>
public sealed class ApolloDbContextRegistrationTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task AddApolloDbContext_WhenWritingInsideUserTransaction_ShouldCommit()
    {
        var ct = TestContext.Current.CancellationToken;
        using var host = Fixture.CreateApplicationBuilder().Build();

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApolloDbContext>();
            var ids = scope.ServiceProvider.GetRequiredService<IPlatformIdResolver>();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await new GuildBuilder().Named("Committed").SeedAsync(db, ids, ct);
            await transaction.CommitAsync(ct);
        }

        await using var read = Fixture.CreateDbContext();
        (await read.Guilds.SingleAsync(ct)).Name.ShouldBe("Committed");
    }
}
