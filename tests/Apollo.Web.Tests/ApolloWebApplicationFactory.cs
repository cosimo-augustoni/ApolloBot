using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Tests.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Apollo.Web.Tests;

/// <summary>
/// In-memory <c>Apollo.Web</c> host for endpoint tests. It runs as Development, so the host skips startup steps
/// meant for deployed environments (e.g. waiting for migrations), and gets placeholder Discord OAuth settings.
/// By default the <c>apollo</c> connection string is a dummy that no test opens and data protection keys live in
/// memory; DB tests set <see cref="ConnectionString"/> and <see cref="PersistDataProtectionKeys"/>.
/// The test endpoints in <see cref="TestSignInEndpoints"/> and <see cref="FakeIdentityProvider"/> are added.
/// </summary>
public sealed class ApolloWebApplicationFactory : WebApplicationFactory<Program>
{
    public static readonly string EnvironmentName = Environments.Development;

    public const string OAuthClientId = "123456789012345678";

    public static readonly Uri BaseAddress = new("https://localhost/");

    public string ConnectionString { get; init; } = "Host=localhost;Database=apollo_web_tests";

    public bool PersistDataProtectionKeys { get; init; }

    /// <summary>A client on <see cref="BaseAddress"/> (HTTPS, so secure cookies are sent) that does not follow redirects.</summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = BaseAddress, AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:apollo", ConnectionString);
        builder.UseSetting("Apollo:Platform:Discord:OAuth:ClientId", OAuthClientId);
        builder.UseSetting("Apollo:Platform:Discord:OAuth:ClientSecret", "placeholder-secret");

        builder.ConfigureTestServices(services =>
        {
            if (!PersistDataProtectionKeys)
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            }

            services.AddSingleton<IPlatformIdentityProvider, FakeIdentityProvider>();
            services.AddTransient<IStartupFilter, TestSignInEndpoints>();
        });
    }
}
