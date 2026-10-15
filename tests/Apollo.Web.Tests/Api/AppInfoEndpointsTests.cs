using System.Net;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Tests.Api;

public sealed class AppInfoEndpointsTests(ApolloWebApplicationFactory factory) : IClassFixture<ApolloWebApplicationFactory>
{
    [Fact]
    public async Task GetAppInfo_ShouldReturnVersionAndEnvironment()
    {
        using var http = factory.CreateClient();

        using var response = await http.GetAsync(new Uri("/api/app-info", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var appInfo = await response.Content.ReadFromJsonAsync<AppInfoDto>(TestContext.Current.CancellationToken);
        appInfo.ShouldNotBeNull();
        appInfo.Version.ShouldNotBeNullOrWhiteSpace();
        appInfo.Version.ShouldNotContain('+');
        appInfo.Environment.ShouldBe(ApolloWebApplicationFactory.EnvironmentName);
    }
}
