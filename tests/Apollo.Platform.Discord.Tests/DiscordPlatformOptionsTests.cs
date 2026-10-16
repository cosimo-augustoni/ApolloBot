namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordPlatformOptionsTests
{
    [Theory]
    [InlineData("Token")]
    [InlineData("GuildId")]
    public void Start_WhenSettingIsMissing_ShouldFailWithValidationErrorNamingTheKey(string key)
    {
        var settings = DiscordHost.ValidSettings();
        settings.Remove($"{DiscordPlatformOptions.SectionName}:{key}");
        using var host = DiscordHost.Build(settings);

        var exception = Should.Throw<Exception>(() => DiscordHost.ValidateOnStart(host));

        exception.ToString().ShouldContain($"Apollo:Platform:Discord:{key} is required");
    }

    [Fact]
    public void Start_WhenSettingsAreValid_ShouldPassValidation()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        Should.NotThrow(() => DiscordHost.ValidateOnStart(host));
    }
}
