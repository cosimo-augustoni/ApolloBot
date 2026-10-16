using Apollo.Platform.Abstractions;

namespace Apollo.Testing.Platform;

/// <summary>
/// A settable <see cref="IPlatformConnectionStatus"/>: disconnected since creation until <see cref="Connect"/>.
/// </summary>
/// <param name="timeProvider">Stamps <see cref="PlatformConnectionSnapshot.StateChangedAt"/>.</param>
/// <param name="guild">The configured guild; defaults to <see cref="TestPlatform.Guild"/>.</param>
public sealed class FakePlatformConnectionStatus(TimeProvider timeProvider, PlatformRef? guild = null) : IPlatformConnectionStatus
{
    public PlatformRef Guild { get; } = guild ?? TestPlatform.Guild;

    public PlatformConnectionSnapshot Current { get; private set; } = new(false, timeProvider.GetUtcNow(), null);

    public FakePlatformConnectionStatus Connect(TimeSpan? latency = null)
    {
        Current = new PlatformConnectionSnapshot(true, timeProvider.GetUtcNow(), latency);
        return this;
    }

    public FakePlatformConnectionStatus Disconnect()
    {
        Current = new PlatformConnectionSnapshot(false, timeProvider.GetUtcNow(), null);
        return this;
    }
}
