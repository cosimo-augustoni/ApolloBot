using System.Data.Common;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Apollo.IntegrationTests.Data;

/// <summary>
/// Fails the first <c>failures</c> connection attempts, as an unreachable (transient) or a broken (non-transient)
/// database would.
/// </summary>
public sealed class UnavailableDatabaseInterceptor(int failures, bool transient) : DbConnectionInterceptor
{
    private int _attempts;

    public int Attempts => _attempts;

    public override InterceptionResult ConnectionOpening(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
    {
        Fail();
        return result;
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Fail();
        return ValueTask.FromResult(result);
    }

    private void Fail()
    {
        if (Interlocked.Increment(ref _attempts) > failures)
        {
            return;
        }

        throw transient
            ? new NpgsqlException("Simulated: database unreachable.", new SocketException((int)SocketError.ConnectionRefused))
            : new NpgsqlException("Simulated: non-transient failure.");
    }
}
