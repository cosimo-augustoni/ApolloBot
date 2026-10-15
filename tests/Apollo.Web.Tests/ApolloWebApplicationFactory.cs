using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Apollo.Web.Tests;

/// <summary>
/// In-memory <c>Apollo.Web</c> host for endpoint tests. It runs as Development, so the host skips startup steps
/// meant for deployed environments (e.g. waiting for migrations), and gets a dummy <c>apollo</c> connection string
/// so the DbContext registration resolves. No test opens a database connection.
/// </summary>
public sealed class ApolloWebApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly string EnvironmentName = Environments.Development;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:apollo", "Host=localhost;Database=apollo_web_tests");
    }
}
