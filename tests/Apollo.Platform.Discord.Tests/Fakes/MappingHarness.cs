using Apollo.Testing;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// The real <see cref="DiscordEventMapper"/> (with its guild filter) in front of a <see cref="RecordingEventDispatcher"/>,
/// plus the fixed test clock, for the gateway handler tests.
/// </summary>
internal sealed class MappingHarness
{
    public RecordingEventDispatcher Dispatcher { get; } = new();

    public TimeProvider Time { get; } = TestTime.CreateProvider();

    public DiscordEventMapper Mapper => field ??= new(
        Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId }),
        Dispatcher,
        new FakeLogger<DiscordEventMapper>());
}
