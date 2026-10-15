using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Npgsql;

namespace Apollo.IntegrationTests;

public sealed class AppHostTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    [Fact]
    public async Task AppHost_WhenStarted_ShouldReportWebHealthyAndBotRunning()
    {
        using var cts = CreateTimeout();

        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("web", cts.Token);
        await fixture.App.ResourceNotifications.WaitForResourceAsync("bot", KnownResourceStates.Running, cts.Token);

        using var http = fixture.App.CreateHttpClient("web");
        using var response = await http.GetAsync(new Uri("/health", UriKind.Relative), cts.Token);

        response.IsSuccessStatusCode.ShouldBeTrue($"GET /health returned {(int)response.StatusCode}");
    }

    [Fact]
    public async Task AppHost_WhenStarted_ShouldProvideApolloDatabase()
    {
        using var cts = CreateTimeout();

        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("apollo", cts.Token);
        var connectionString = await fixture.App.GetConnectionStringAsync("apollo", cts.Token);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cts.Token);

        connection.Database.ShouldBe("apollo");

        await using var select = new NpgsqlCommand("SELECT 1", connection);
        (await select.ExecuteScalarAsync(cts.Token)).ShouldBe(1);

        await using var timescale = new NpgsqlCommand(
            "SELECT default_version FROM pg_available_extensions WHERE name = 'timescaledb'", connection);
        (await timescale.ExecuteScalarAsync(cts.Token)).ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(AppHostFixture.StartupTimeout);
        return cts;
    }
}
