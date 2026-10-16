using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Apollo.IntegrationTests;

/// <summary>
/// Starts the AppHost once for all smoke tests in a class.
/// The <c>db</c> container is switched to a session lifetime without volumes, so tests never reuse
/// (or leave behind) the persistent dev container and its data. The bot gets placeholder Discord settings and does not
/// connect to Discord; the web host gets a placeholder OAuth client.
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
        UseOfflineBot(builder);
        UsePlaceholderOAuthClient(builder);

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

    // Environment variables win over the bot's user-secrets, so a developer's real token is never used here.
    private static void UseOfflineBot(IDistributedApplicationTestingBuilder builder) =>
        builder.CreateResourceBuilder<ProjectResource>("bot")
            .WithEnvironment("Apollo__Platform__Discord__Token", "MTIzNDU2Nzg5MDEyMzQ1Njc4.placeholder.token")
            .WithEnvironment("Apollo__Platform__Discord__GuildId", "111111111111111111")
            .WithEnvironment("Apollo__Platform__Discord__Connect", "false");

    // The login is never exercised here; web only needs settings that pass its startup validation.
    private static void UsePlaceholderOAuthClient(IDistributedApplicationTestingBuilder builder) =>
        builder.CreateResourceBuilder<ProjectResource>("apollo-web")
            .WithEnvironment("Apollo__Platform__Discord__OAuth__ClientId", "123456789012345678")
            .WithEnvironment("Apollo__Platform__Discord__OAuth__ClientSecret", "placeholder-secret");

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
