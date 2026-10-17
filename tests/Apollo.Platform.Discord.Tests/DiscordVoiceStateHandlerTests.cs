using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordVoiceStateHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;
    private const ulong AliceId = 42;
    private const ulong LoungeId = 600;

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task VoiceStateUpdate_WhenUserIsInAChannel_ShouldDispatchTheirStateStampedOnReceipt()
    {
        await HandleAsync(NetCordPayloads.VoiceState(DiscordHost.GuildId, AliceId, LoungeId, selfMute: true, selfDeaf: true, streaming: true, video: true));

        var changed = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformVoiceStateChanged>();
        changed.Guild.ShouldBe(Ref(DiscordHost.GuildId));
        changed.OccurredAt.ShouldBe(TestTime.Now);
        changed.State.ShouldBe(new PlatformVoiceState(
            User: Ref(AliceId),
            Channel: Ref(LoungeId),
            ConnectionId: null,
            SelfMute: true,
            SelfDeaf: true,
            Streaming: true,
            Video: true));
    }

    [Fact]
    public async Task VoiceStateUpdate_WhenUserLeftVoice_ShouldDispatchAStateWithoutChannel()
    {
        await HandleAsync(NetCordPayloads.VoiceState(DiscordHost.GuildId, AliceId, channelId: null));

        var changed = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformVoiceStateChanged>();
        changed.State.Channel.ShouldBeNull();
    }

    [Fact]
    public async Task VoiceStateUpdate_WhenOnlyServerSideFieldsAreSet_ShouldLeaveTheSelfFlagsOff()
    {
        await HandleAsync(NetCordPayloads.VoiceState(DiscordHost.GuildId, AliceId, LoungeId, serverMute: true));

        var state = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformVoiceStateChanged>().State;
        (state.SelfMute, state.SelfDeaf, state.Streaming, state.Video).ShouldBe((false, false, false, false));
    }

    [Fact]
    public async Task VoiceStateUpdate_WhenFromAnotherGuild_ShouldDispatchNothing()
    {
        await HandleAsync(NetCordPayloads.VoiceState(ForeignGuildId, AliceId, LoungeId));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private ValueTask HandleAsync(NetCord.Gateway.VoiceState state) =>
        ((IVoiceStateUpdateGatewayHandler)new DiscordVoiceStateHandler(_harness.Mapper, _harness.Time)).HandleAsync(state);
}
