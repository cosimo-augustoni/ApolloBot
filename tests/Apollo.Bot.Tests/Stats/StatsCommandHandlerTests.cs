using Apollo.Bot.Resources;
using Apollo.Bot.Tests.Tracking;
using Apollo.Platform.Abstractions.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using static Apollo.Bot.Tests.Stats.StatsCommandHost;

namespace Apollo.Bot.Tests.Stats;

public sealed class StatsCommandHandlerTests
{
    [Theory]
    [InlineData("https://apollo.example.ch")]
    [InlineData("https://apollo.example.ch/")]
    public async Task Stats_WhenPublicWebUrlHasOrLacksATrailingSlash_ShouldReplyPrivatelyWithExactlyTheMePageUrl(string publicWebUrl)
    {
        using var host = Create(publicWebUrl);

        var reply = await host.RunAsync(Alice);

        reply.Visibility.ShouldBe(ReplyVisibility.Private);
        reply.Body().ShouldBe("Deine Statistiken findest du hier: https://apollo.example.ch/me");
    }

    [Fact]
    public async Task Stats_ShouldReplyWithTheResourceTextAndNoStatsValues()
    {
        const string MePageUrl = "https://apollo.example.ch/me";
        using var host = Create("https://apollo.example.ch");
        var strings = host.Services.GetRequiredService<IStringLocalizer<Strings>>();

        var reply = await host.RunAsync(Alice);

        var body = reply.Body();
        body.ShouldBe(strings["Stats_Link", MePageUrl].Value);
        body.Replace(MePageUrl, "", StringComparison.Ordinal).ShouldNotContain(c => char.IsDigit(c));
    }

    [Fact]
    public async Task Stats_ShouldEndWithThePrivacyFooterFromTheRouter()
    {
        using var host = Create("https://apollo.example.ch");

        var reply = await host.RunAsync(Alice);

        reply.Text.ShouldEndWith("\n\nDatenschutz: https://apollo.example.ch/datenschutz");
    }
}
