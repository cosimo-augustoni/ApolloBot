using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformGuildAvailableBuilder : PlatformEventBuilder<PlatformGuildAvailableBuilder, PlatformGuildAvailable>
{
    public PlatformGuildAvailableBuilder Named(string name) => With(e => e with { Name = name });

    public PlatformGuildAvailableBuilder OwnedBy(PlatformRef owner) => With(e => e with { Owner = owner });

    public PlatformGuildAvailableBuilder WithRoles(params PlatformRole[] roles) => With(e => e with { Roles = roles });

    public PlatformGuildAvailableBuilder WithChannels(params PlatformChannel[] channels) => With(e => e with { Channels = channels });

    public PlatformGuildAvailableBuilder WithMembers(params PlatformMember[] members) => With(e => e with { Members = members });

    public PlatformGuildAvailableBuilder WithVoiceStates(params PlatformVoiceState[] voiceStates) => With(e => e with { VoiceStates = voiceStates });

    public PlatformGuildAvailableBuilder WithPresences(params PlatformPresence[] presences) => With(e => e with { Presences = presences });

    protected override PlatformGuildAvailable CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Name: "Apollo Test Guild",
        Owner: TestPlatform.User("owner"),
        Roles: [new PlatformRoleBuilder().Build()],
        Channels: [new PlatformChannelBuilder().Build()],
        Members: [new PlatformMemberBuilder().Build()],
        VoiceStates: [],
        Presences: []);
}
