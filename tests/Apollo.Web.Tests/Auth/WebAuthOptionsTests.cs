using Apollo.Web.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Tests.Auth;

public sealed class WebAuthOptionsTests
{
    [Fact]
    public void RevalidationInterval_WhenNotConfigured_ShouldBeFiveMinutes()
    {
        using var factory = new ApolloWebApplicationFactory();

        factory.Services.GetRequiredService<IOptions<WebAuthOptions>>().Value.RevalidationInterval.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void RevalidationInterval_ShouldBindFromApolloWebAuth()
    {
        using var factory = new ApolloWebApplicationFactory().WithWebHostBuilder(b => b.UseSetting("Apollo:Web:Auth:RevalidationInterval", "00:01:30"));

        factory.Services.GetRequiredService<IOptions<WebAuthOptions>>().Value.RevalidationInterval.ShouldBe(TimeSpan.FromSeconds(90));
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("2.00:00:00")]
    public void Start_WhenRevalidationIntervalIsOutOfRange_ShouldFailNamingTheKey(string interval)
    {
        using var factory = new ApolloWebApplicationFactory().WithWebHostBuilder(b => b.UseSetting("Apollo:Web:Auth:RevalidationInterval", interval));

        var exception = Should.Throw<OptionsValidationException>(() => factory.Services);

        exception.Message.ShouldContain("Apollo:Web:Auth:RevalidationInterval must be between");
    }
}
