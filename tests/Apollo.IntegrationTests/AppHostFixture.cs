using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Apollo.IntegrationTests;

/// <summary>
/// Starts the AppHost once for all smoke tests in a class.
/// The <c>db</c> container is switched to a session lifetime without volumes, so tests never reuse
/// (or leave behind) the persistent dev container and its data.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    public static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    private DistributedApplication? _app;

    public DistributedApplication App => _app ?? throw new InvalidOperationException("The AppHost has not been started.");

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(StartupTimeout);

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Apollo_AppHost>(cts.Token);
        UseThrowawayDatabase(builder);

        _app = await builder.BuildAsync(cts.Token);
        await _app.StartAsync(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static void UseThrowawayDatabase(IDistributedApplicationTestingBuilder builder)
    {
        // WithSessionLifetime is marked as test-only by Aspire, which is exactly this use.
#pragma warning disable ASPIREPERSISTENCE001
        var db = builder.CreateResourceBuilder<PostgresServerResource>("db").WithSessionLifetime();
#pragma warning restore ASPIREPERSISTENCE001

        foreach (var volume in db.Resource.Annotations.OfType<ContainerMountAnnotation>().ToList())
        {
            db.Resource.Annotations.Remove(volume);
        }
    }
}
