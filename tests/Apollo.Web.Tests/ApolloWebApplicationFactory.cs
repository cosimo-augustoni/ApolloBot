using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Apollo.Web.Tests;

/// <summary>In-memory <c>Apollo.Web</c> host for endpoint tests, running in the <see cref="EnvironmentName"/> environment.</summary>
public sealed class ApolloWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string EnvironmentName = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(EnvironmentName);
}
