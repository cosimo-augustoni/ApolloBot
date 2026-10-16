using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Tests;

/// <summary>
/// Builds (never starts) a host with <see cref="DiscordPlatformServiceCollectionExtensions.AddDiscordPlatform"/>, so
/// tests can inspect the registrations without connecting to Discord.
/// </summary>
internal static class DiscordHost
{
    public const string Token = "MTIzNDU2Nzg5MDEyMzQ1Njc4.test.token";

    public const ulong GuildId = 111111111111111111;

    public static Dictionary<string, string?> ValidSettings() => new()
    {
        [$"{DiscordPlatformOptions.SectionName}:Token"] = Token,
        [$"{DiscordPlatformOptions.SectionName}:GuildId"] = GuildId.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static IHost Build(IDictionary<string, string?> settings)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddDiscordPlatform();
        return builder.Build();
    }

    /// <summary>
    /// Runs the same options validation as <see cref="IHost.StartAsync"/>, without starting the gateway client.
    /// </summary>
    public static void ValidateOnStart(IHost host) => host.Services.GetRequiredService<IStartupValidator>().Validate();
}
