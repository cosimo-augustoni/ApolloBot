using CmdScale.EntityFrameworkCore.TimescaleDB;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Data;

/// <summary>
/// The one database all Apollo processes share (scope §5.2). Schema changes are migrations in this project,
/// applied by Apollo.MigrationService.
/// </summary>
public sealed class ApolloDbContext(DbContextOptions<ApolloDbContext> options) : DbContext(options)
{
    /// <summary>
    /// The Aspire resource name, and so the key under <c>ConnectionStrings</c>.
    /// </summary>
    public const string ConnectionName = "apollo";

    /// <summary>
    /// Options for a context outside dependency injection (design time, tests).
    /// </summary>
    public static DbContextOptions<ApolloDbContext> CreateOptions(string connectionString) =>
        new DbContextOptionsBuilder<ApolloDbContext>()
            .UseNpgsql(connectionString)
            .UseTimescaleDb()
            .Options;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CmdScale never emits CREATE EXTENSION; this makes the first migration do it (docs/adr/0001-timescale-ef.md).
        modelBuilder.HasPostgresExtension("timescaledb");
    }
}
