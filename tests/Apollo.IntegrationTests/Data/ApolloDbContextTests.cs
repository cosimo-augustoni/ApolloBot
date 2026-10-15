using Apollo.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data;

public sealed class ApolloDbContextTests
{
    [Fact]
    public void Model_WhenAllMigrationsAreAdded_ShouldHaveNoPendingChanges()
    {
        using var db = new ApolloDbContextFactory().CreateDbContext([]);

        db.Database.HasPendingModelChanges().ShouldBeFalse("run `dotnet ef migrations add` (CLAUDE.md)");
    }
}
