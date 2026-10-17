using Apollo.Web.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Tests.Admin;

public sealed class BotStatusOptionsTests
{
    [Fact]
    public void StaleAfter_WhenNotConfigured_ShouldBeTwoMinutes()
    {
        using var factory = new ApolloWebApplicationFactory();

        factory.Services.GetRequiredService<IOptions<BotStatusOptions>>().Value.StaleAfter.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void StaleAfter_ShouldBindFromApolloWebBotStatus()
    {
        using var factory = new ApolloWebApplicationFactory().WithWebHostBuilder(b => b.UseSetting("Apollo:Web:BotStatus:StaleAfter", "00:05:00"));

        factory.Services.GetRequiredService<IOptions<BotStatusOptions>>().Value.StaleAfter.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [InlineData("00:00:30")]
    [InlineData("2.00:00:00")]
    public void Start_WhenStaleAfterIsOutOfRange_ShouldFailNamingTheKey(string staleAfter)
    {
        using var factory = new ApolloWebApplicationFactory().WithWebHostBuilder(b => b.UseSetting("Apollo:Web:BotStatus:StaleAfter", staleAfter));

        var exception = Should.Throw<OptionsValidationException>(() => factory.Services);

        exception.Message.ShouldContain("Apollo:Web:BotStatus:StaleAfter must be between");
    }
}
