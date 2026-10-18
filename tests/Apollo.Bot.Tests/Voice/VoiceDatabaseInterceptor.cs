using System.Data.Common;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Apollo.Bot.Tests.Voice;

/// <summary>
/// Simulates database failures for the voice recorder: <see cref="FailNext"/> fails the next command that starts with a
/// given text (e.g. closing a session), <see cref="GoDown"/> fails every command until <see cref="ComeBack"/>, as an
/// unreachable database would. Counts the voice writes that reached the database (<see cref="VoiceWrites"/>).
/// </summary>
internal sealed class VoiceDatabaseInterceptor : DbCommandInterceptor
{
    public const string CloseSession = "UPDATE voice_sessions";
    public const string OpenSession = "INSERT INTO voice_sessions";

    private readonly Lock _lock = new();
    private string? _failNext;
    private bool _down;
    private int _voiceWrites;

    public int VoiceWrites => Volatile.Read(ref _voiceWrites);

    public void FailNext(string commandStart)
    {
        lock (_lock)
        {
            _failNext = commandStart;
        }
    }

    public void GoDown()
    {
        lock (_lock)
        {
            _down = true;
        }
    }

    public void ComeBack()
    {
        lock (_lock)
        {
            _down = false;
        }
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        Check(command);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        Check(command);
        return ValueTask.FromResult(result);
    }

    private void Check(DbCommand command)
    {
        var text = command.CommandText.TrimStart();
        lock (_lock)
        {
            if (_down)
            {
                throw new NpgsqlException("Simulated: database unreachable.", new SocketException((int)SocketError.TimedOut));
            }

            if (_failNext is { } start && text.StartsWith(start, StringComparison.OrdinalIgnoreCase))
            {
                _failNext = null;
                throw new InvalidOperationException("Simulated: database write failed.");
            }
        }

        if (text.StartsWith("INSERT INTO voice_", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("UPDATE voice_", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Increment(ref _voiceWrites);
        }
    }
}
