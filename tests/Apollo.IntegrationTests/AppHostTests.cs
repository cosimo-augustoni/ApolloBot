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

        await fixture.App.ResourceNotifications.WaitForResourceHealthyAsync("apollo-web", cts.Token);
        await fixture.App.ResourceNotifications.WaitForResourceAsync("bot", KnownResourceStates.Running, cts.Token);

        using var http = fixture.App.CreateHttpClient("apollo-web");
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

    [Fact]
    public async Task AppHost_WhenStarted_ShouldRunMigrationServiceToCompletionBeforeBotAndWeb()
    {
        using var cts = CreateTimeout();

        var migrations = await fixture.App.ResourceNotifications.WaitForResourceAsync(
            "migrationservice", e => KnownResourceStates.TerminalStates.Contains(e.Snapshot.State?.Text), cts.Token);
        var bot = await fixture.App.ResourceNotifications.WaitForResourceAsync(
            "bot", e => e.Snapshot.State?.Text == KnownResourceStates.Running, cts.Token);
        var web = await fixture.App.ResourceNotifications.WaitForResourceAsync(
            "apollo-web", e => e.Snapshot.State?.Text == KnownResourceStates.Running, cts.Token);

        migrations.Snapshot.State?.Text.ShouldBe(KnownResourceStates.Finished);
        migrations.Snapshot.ExitCode.ShouldBe(0);
        var finishedAt = migrations.Snapshot.StopTimeStamp.ShouldNotBeNull();
        bot.Snapshot.StartTimeStamp.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(finishedAt);
        web.Snapshot.StartTimeStamp.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(finishedAt);
    }

    [Fact]
    public async Task AppHost_WhenStarted_ShouldMigrateApolloDatabase()
    {
        using var cts = CreateTimeout();

        await fixture.App.ResourceNotifications.WaitForResourceAsync("migrationservice", KnownResourceStates.Finished, cts.Token);
        var connectionString = await fixture.App.GetConnectionStringAsync("apollo", cts.Token);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cts.Token);

        await using var history = new NpgsqlCommand("""SELECT "MigrationId" FROM "__EFMigrationsHistory" """, connection);
        (await history.ExecuteScalarAsync(cts.Token)).ShouldBeOfType<string>().ShouldEndWith("_InitialCreate");

        await using var extension = new NpgsqlCommand("SELECT extversion FROM pg_extension WHERE extname = 'timescaledb'", connection);
        (await extension.ExecuteScalarAsync(cts.Token)).ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(AppHostFixture.StartupTimeout);
        return cts;
    }
}
