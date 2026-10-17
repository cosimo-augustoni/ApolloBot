using Apollo.Bot.Commands;
using Apollo.Bot.Links;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Commands;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// A <see cref="TrackingHost"/> with the real command routing and the tracking commands, its
/// <see cref="FakeChatPlatform"/> wired to the router, and a guild where alice holds the tracking role
/// <see cref="Statistik"/>, bob is an Administrator without it and carol has neither.
/// </summary>
internal static class TrackingCommandHost
{
    public const string PublicWebUrl = "https://apollo.example.test";

    public static readonly PlatformRef Statistik = TestPlatform.Role("statistik");
    public static readonly PlatformRef Admin = TestPlatform.Role("admin");
    public static readonly PlatformRef Alice = TestPlatform.User("alice");
    public static readonly PlatformRef Bob = TestPlatform.User("bob");
    public static readonly PlatformRef Carol = TestPlatform.User("carol");

    public static TrackingHost Create(TimescaleFixture fixture)
    {
        var host = new TrackingHost(fixture, services => services
            .AddCommandRouting()
            .Configure<LinksOptions>(links => links.PublicWebUrl = PublicWebUrl)
            .AddTrackingCommands());
        host.Platform.WithCommandRouter(host.Services.GetRequiredService<ICommandRouter>());
        return host;
    }

    /// <summary>
    /// A host whose tracking role is <see cref="Statistik"/>, after the first full guild sync.
    /// </summary>
    public static async Task<TrackingHost> CreateInTrackingRoleModeAsync(TimescaleFixture fixture)
    {
        var host = Create(fixture);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await host.SyncAsync(Snapshot());
        return host;
    }

    public static PlatformGuildAvailable Snapshot(bool withTrackingRole = true)
    {
        var roles = new List<PlatformRole> { new PlatformRoleBuilder().WithRef(Admin).Named("Admin").AsAdministrator().Build() };
        if (withTrackingRole)
        {
            roles.Add(new PlatformRoleBuilder().WithRef(Statistik).Named("Statistik").Build());
        }

        return new PlatformGuildAvailableBuilder()
            .WithRoles([.. roles])
            .WithMembers(
            [
                new PlatformMemberBuilder().ForUser(Alice).WithRoles(withTrackingRole ? [Statistik] : []).Build(),
                new PlatformMemberBuilder().ForUser(Bob).WithRoles(Admin).Build(),
                new PlatformMemberBuilder().ForUser(Carol).Build(),
            ])
            .Build();
    }

    public static Task<CommandReply> RunAsync(this TrackingHost host, PlatformRef member, string path) =>
        host.Platform.InvokeCommandAsync(member, path, TestContext.Current.CancellationToken);

    /// <summary>
    /// The handler's text: the reply without the privacy footer the router appends.
    /// </summary>
    public static string Body(this CommandReply reply) => reply.Text[..reply.Text.LastIndexOf("\n\n", StringComparison.Ordinal)];
}
