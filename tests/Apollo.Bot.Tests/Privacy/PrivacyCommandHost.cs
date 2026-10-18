using Apollo.Bot.Commands;
using Apollo.Bot.Links;
using Apollo.Bot.Presence;
using Apollo.Bot.Privacy;
using Apollo.Bot.Tests.Tracking;
using Apollo.Bot.Tracking;
using Apollo.Bot.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// A <see cref="TrackingHost"/> with the real command routing, the tracking and privacy commands and the voice and
/// presence recorders, on the guild of <see cref="TrackingCommandHost"/>: alice holds the tracking role
/// <see cref="TrackingCommandHost.Statistik"/>, bob is an Administrator without it and carol has neither. A test's own
/// <c>configure</c> runs after these registrations, to replace one of them.
/// </summary>
internal static class PrivacyCommandHost
{
    public const string DeletePath = "apollo privacy delete";

    public static TrackingHost CreatePrivacyHost(TimescaleFixture fixture, IInterceptor? interceptor = null, Action<IServiceCollection>? configure = null)
    {
        var host = new TrackingHost(
            fixture,
            services =>
            {
                services
                    .AddCommandRouting()
                    .Configure<LinksOptions>(links => links.PublicWebUrl = PublicWebUrl)
                    .AddTrackingCommands()
                    .AddPrivacyCommands()
                    .AddVoiceRecording()
                    .AddPresenceRecording();
                configure?.Invoke(services);
            },
            interceptor);
        host.Platform.WithCommandRouter(host.Services.GetRequiredService<ICommandRouter>());
        return host;
    }

    /// <summary>
    /// A host whose tracking role is <see cref="TrackingCommandHost.Statistik"/>, after the first full guild sync.
    /// </summary>
    public static async Task<TrackingHost> CreatePrivacyHostInTrackingRoleModeAsync(
        TimescaleFixture fixture,
        IInterceptor? interceptor = null,
        Action<IServiceCollection>? configure = null)
    {
        var host = CreatePrivacyHost(fixture, interceptor, configure);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot());
        return host;
    }

    /// <summary>
    /// <paramref name="member"/> runs <c>/apollo privacy delete</c> and confirms with the button of the reply.
    /// </summary>
    public static async Task<CommandReply> ConfirmDeleteAsync(this TrackingHost host, PlatformRef member)
    {
        var prompt = await host.RunAsync(member, DeletePath);
        return await host.Platform.ClickButtonAsync(member, prompt.Buttons.Single().Id, TestContext.Current.CancellationToken);
    }
}
