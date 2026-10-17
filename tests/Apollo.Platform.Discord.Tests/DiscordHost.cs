using System.Reflection;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Discord.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

/// <summary>
/// Builds (never starts) a host with <see cref="DiscordPlatformServiceCollectionExtensions.AddDiscordPlatform"/>, so
/// tests can inspect the registrations without connecting to Discord. A stub stands in for the bot's command router.
/// </summary>
internal static class DiscordHost
{
    public const string Token = "MTIzNDU2Nzg5MDEyMzQ1Njc4.test.token";

    /// <summary>The bot's user ID that <see cref="Token"/> encodes (its first part is the ID in base64).</summary>
    public const ulong BotUserId = 123456789012345678;

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
        builder.Services.AddSingleton<ICommandRouter, StubCommandRouter>();
        return builder.Build();
    }

    /// <summary>
    /// Runs the same options validation as <see cref="IHost.StartAsync"/>, without starting the gateway client.
    /// </summary>
    public static void ValidateOnStart(IHost host) => host.Services.GetRequiredService<IStartupValidator>().Validate();

    /// <summary>
    /// The gateway handlers NetCord.Hosting would subscribe, created through the factories it registered (so this
    /// also proves DI can build them). NetCord.Hosting registers them as its internal <c>IGatewayHandlerMetadata</c>
    /// with the factory in a non-public property, so this reads them by reflection.
    /// </summary>
    public static IReadOnlyList<object> GatewayHandlers(IHost host) =>
    [
        .. host.Services.GetServices(typeof(GatewayClientOptions).Assembly.GetType("NetCord.Hosting.IGatewayHandlerMetadata", throwOnError: true)!)
            .Select(metadata => metadata!.GetType()
                .GetProperty("InstanceFactory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(metadata))
            .OfType<Func<IServiceProvider, object>>()
            .Select(factory => factory(host.Services)),
    ];
}
