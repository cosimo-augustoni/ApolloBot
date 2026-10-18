using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Web.Client.Tests.Api;

public sealed class ApiClientServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApolloApiClients_WhenUseMockDataTrue_ShouldRegisterMockClient()
    {
        using var provider = BuildProvider(useMockData: "true", new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IAppInfoClient>().ShouldBeOfType<MockAppInfoClient>();
    }

    [Fact]
    public void AddApolloApiClients_WhenUseMockDataFalse_ShouldRegisterHttpClient()
    {
        using var provider = BuildProvider(useMockData: "false", new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IAppInfoClient>().ShouldBeOfType<HttpAppInfoClient>();
    }

    [Fact]
    public void AddApolloApiClients_WhenSettingMissing_ShouldRegisterHttpClient()
    {
        using var provider = BuildProvider(useMockData: null, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IAppInfoClient>().ShouldBeOfType<HttpAppInfoClient>();
    }

    [Theory]
    [InlineData("true", typeof(MockRoleSettingsClient))]
    [InlineData("false", typeof(HttpRoleSettingsClient))]
    public void AddApolloApiClients_ShouldRegisterTheRoleSettingsClientOfTheSetting(string useMockData, Type expected)
    {
        using var provider = BuildProvider(useMockData, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IRoleSettingsClient>().ShouldBeOfType(expected);
    }

    [Theory]
    [InlineData("true", typeof(MockBotStatusClient))]
    [InlineData("false", typeof(HttpBotStatusClient))]
    public void AddApolloApiClients_ShouldRegisterTheBotStatusClientOfTheSetting(string useMockData, Type expected)
    {
        using var provider = BuildProvider(useMockData, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IBotStatusClient>().ShouldBeOfType(expected);
    }

    [Theory]
    [InlineData("true", typeof(MockServerStatsClient))]
    [InlineData("false", typeof(HttpServerStatsClient))]
    public void AddApolloApiClients_ShouldRegisterTheServerStatsClientOfTheSetting(string useMockData, Type expected)
    {
        using var provider = BuildProvider(useMockData, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IServerStatsClient>().ShouldBeOfType(expected);
    }

    [Theory]
    [InlineData("true", typeof(MockDataSanityClient))]
    [InlineData("false", typeof(HttpDataSanityClient))]
    public void AddApolloApiClients_ShouldRegisterTheDataSanityClientOfTheSetting(string useMockData, Type expected)
    {
        using var provider = BuildProvider(useMockData, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IDataSanityClient>().ShouldBeOfType(expected);
    }

    [Theory]
    [InlineData("true", typeof(MockMemberStatsClient))]
    [InlineData("false", typeof(HttpMemberStatsClient))]
    public void AddApolloApiClients_ShouldRegisterTheMemberStatsClientOfTheSetting(string useMockData, Type expected)
    {
        using var provider = BuildProvider(useMockData, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IMemberStatsClient>().ShouldBeOfType(expected);
    }

    [Theory]
    [InlineData("true", typeof(MockDataOverviewClient))]
    [InlineData("false", typeof(HttpDataOverviewClient))]
    public void AddApolloApiClients_ShouldRegisterTheDataOverviewClientOfTheSetting(string useMockData, Type expected)
    {
        using var provider = BuildProvider(useMockData, new StubHttpMessageHandler(new object()));

        provider.GetRequiredService<IDataOverviewClient>().ShouldBeOfType(expected);
    }

    [Fact]
    public async Task HttpAppInfoClient_ShouldReadAppInfoFromApi()
    {
        var expected = new AppInfoDto("1.2.3", "Production");
        var handler = new StubHttpMessageHandler(expected);
        using var provider = BuildProvider(useMockData: "false", handler);

        var appInfo = await provider.GetRequiredService<IAppInfoClient>().GetAppInfoAsync(TestContext.Current.CancellationToken);

        appInfo.ShouldBe(expected);
        handler.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/app-info");
    }

    private static ServiceProvider BuildProvider(string? useMockData, StubHttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{ApiClientOptions.SectionName}:UseMockData"] = useMockData })
            .Build();

        var services = new ServiceCollection();
        services.AddScoped(_ => handler.CreateClient());
        services.AddApolloApiClients(configuration);
        return services.BuildServiceProvider();
    }
}
