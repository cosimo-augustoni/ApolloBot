using Microsoft.Extensions.Hosting;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// An <see cref="IHostApplicationLifetime"/> a test can stop, standing in for the bot host's.
/// </summary>
internal sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => _stopping.Token;

    public CancellationToken ApplicationStopped => CancellationToken.None;

    public void StopApplication() => _stopping.Cancel();

    public void Dispose() => _stopping.Dispose();
}
