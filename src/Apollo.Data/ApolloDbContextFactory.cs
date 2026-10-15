using Microsoft.EntityFrameworkCore.Design;

namespace Apollo.Data;

/// <summary>
/// Used by <c>dotnet ef migrations add</c>, so it works without a running AppHost. Generating a migration does not
/// open a connection.
/// </summary>
public sealed class ApolloDbContextFactory : IDesignTimeDbContextFactory<ApolloDbContext>
{
    public ApolloDbContext CreateDbContext(string[] args) =>
        new(ApolloDbContext.CreateOptions("Host=localhost;Database=apollo_design_time"));
}
