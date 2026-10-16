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
    [InlineData("https://apollo.example.test")]
    [InlineData("http://localhost:5123")]
    public void Start_WhenPublicWebUrlIsAnAbsoluteHttpUrl_ShouldPassValidation(string url)
    {
        var settings = CommandTestHost.ValidSettings();
        settings["Apollo:Links:PublicWebUrl"] = url;
        using var host = CommandTestHost.Build(settings);

        Should.NotThrow(() => ValidateOnStart(host));
    }

    // Runs the same options validation as IHost.StartAsync, without starting anything.
    private static void ValidateOnStart(Microsoft.Extensions.Hosting.IHost host) =>
        host.Services.GetRequiredService<IStartupValidator>().Validate();
}
