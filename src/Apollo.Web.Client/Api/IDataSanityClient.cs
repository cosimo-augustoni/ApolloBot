using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>The data sanity report for the admin page (#40, <c>GET /api/admin/sanity</c>).</summary>
public interface IDataSanityClient
{
    /// <summary>
    /// The report from <paramref name="from"/> to <paramref name="to"/> (Zurich days, both included); without a range the
    /// host checks the last 30 days up to today.
    /// </summary>
    Task<DataSanityDto> GetAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}
