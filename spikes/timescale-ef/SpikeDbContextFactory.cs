using Microsoft.EntityFrameworkCore.Design;

namespace Apollo.Spikes.TimescaleEf;

/// <summary>
/// Used by <c>dotnet ef migrations add</c> only; generating a migration does not open a connection.
/// </summary>
public sealed class SpikeDbContextFactory : IDesignTimeDbContextFactory<SpikeDbContext>
{
    public SpikeDbContext CreateDbContext(string[] args) =>
        new(SpikeDbContext.CreateOptions("Host=localhost;Database=design_time"));
}
