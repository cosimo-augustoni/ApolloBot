namespace Apollo.Stats.Sanity;

/// <summary>
/// The data sanity report of the admin area (#40, scope §7.2): checks that show collection bugs while the first stats
/// are watched. Each check returns a count and up to <see cref="DataSanityQuery.MaxExamples"/> example rows.
/// </summary>
public interface IDataSanityService
{
    /// <exception cref="ArgumentException">The range of <paramref name="query"/> ends before it starts.</exception>
    Task<DataSanityReport> GetAsync(DataSanityQuery query, CancellationToken cancellationToken);
}
