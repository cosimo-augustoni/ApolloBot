using Apollo.Bot.Links;
using Apollo.Bot.Tests.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apollo.Bot.Tests.Links;

public sealed class LinksOptionsTests
{
    [Fact]
    public void Start_WhenPublicWebUrlIsMissing_ShouldFailWithValidationErrorNamingTheKey()
    {
        var settings = CommandTestHost.ValidSettings();
        settings.Remove("Apollo:Links:PublicWebUrl");
        using var host = CommandTestHost.Build(settings);

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(host));

        exception.Message.ShouldContain("Apollo:Links:PublicWebUrl is required");
    }

    [Theory]
    [InlineData("apollo.example.test")]
    [InlineData("/datenschutz")]
    [InlineData("ftp://apollo.example.test")]
    [InlineData("not a url")]
    public void Start_WhenPublicWebUrlIsNotAnAbsoluteHttpUrl_ShouldFailWithValidationError(string url)
    {
        var settings = CommandTestHost.ValidSettings();
        settings["Apollo:Links:PublicWebUrl"] = url;
        using var host = CommandTestHost.Build(settings);

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(host));

        exception.Message.ShouldContain("Apollo:Links:PublicWebUrl must be an absolute http or https URL");
    }

    [Theory]
    [InlineData("https://apollo.example.test/?a=b")]
    [InlineData("https://apollo.example.test/#top")]
    public void Start_WhenPublicWebUrlHasAQueryOrFragment_ShouldFailWithValidationError(string url)
    {
        var settings = CommandTestHost.ValidSettings();
        settings["Apollo:Links:PublicWebUrl"] = url;
        using var host = CommandTestHost.Build(settings);

        var exception = Should.Throw<OptionsValidationException>(() => ValidateOnStart(host));

        exception.Message.ShouldContain("Apollo:Links:PublicWebUrl must not have a query or fragment");
    }

    [Theory]
    [InlineData("https://apollo.example.test")]
    [InlineData("http://localhost:5123")]
    [InlineData("https://example.test/apollo")]
    public void Start_WhenPublicWebUrlIsAnAbsoluteHttpUrl_ShouldPassValidation(string url)
    {
        var settings = CommandTestHost.ValidSettings();
        settings["Apollo:Links:PublicWebUrl"] = url;
        using var host = CommandTestHost.Build(settings);

        Should.NotThrow(() => ValidateOnStart(host));
    }

    [Theory]
    [InlineData("https://apollo.example.ch", "https://apollo.example.ch/me")]
    [InlineData("https://apollo.example.ch/", "https://apollo.example.ch/me")]
    [InlineData("https://example.test/apollo/", "https://example.test/apollo/me")]
    public void GetMePageUrl_WhenPublicWebUrlHasOrLacksATrailingSlash_ShouldJoinItWithMe(string publicWebUrl, string expected)
    {
        var links = new LinksOptions { PublicWebUrl = publicWebUrl };

        links.GetMePageUrl().AbsoluteUri.ShouldBe(expected);
    }

    [Theory]
    [InlineData("https://apollo.example.ch", "https://apollo.example.ch/datenschutz")]
    [InlineData("https://apollo.example.ch/", "https://apollo.example.ch/datenschutz")]
    public void GetPrivacyPageUrl_WhenPublicWebUrlHasOrLacksATrailingSlash_ShouldJoinItWithDatenschutz(string publicWebUrl, string expected)
    {
        var links = new LinksOptions { PublicWebUrl = publicWebUrl };

        links.GetPrivacyPageUrl().AbsoluteUri.ShouldBe(expected);
    }

    // Runs the same options validation as IHost.StartAsync, without starting anything.
    private static void ValidateOnStart(Microsoft.Extensions.Hosting.IHost host) =>
        host.Services.GetRequiredService<IStartupValidator>().Validate();
}
