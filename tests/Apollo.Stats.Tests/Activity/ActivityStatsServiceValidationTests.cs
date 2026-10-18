using Apollo.Data;
using Apollo.Stats.Activity;
using Apollo.Testing;

namespace Apollo.Stats.Tests.Activity;

public sealed class ActivityStatsServiceValidationTests
{
    [Fact]
    public async Task GetAsync_WhenFromIsAfterTo_ShouldThrowAnArgumentException()
    {
        // The context never connects: the query is rejected before it reaches the database.
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions("Host=unused"));
        var service = new ActivityStatsService(db, TestTime.CreateProvider());
        var query = new ActivityStatsQuery(Guid.NewGuid(), null, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 14));

        await Should.ThrowAsync<ArgumentException>(() => service.GetAsync(query, TestContext.Current.CancellationToken));
    }
}
