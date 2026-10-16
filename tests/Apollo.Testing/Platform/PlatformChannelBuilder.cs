using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformChannelBuilder : TestDataBuilder<PlatformChannelBuilder, PlatformChannel>
{
    public PlatformChannelBuilder WithRef(PlatformRef channel) => With(c => c with { Channel = channel });

    public PlatformChannelBuilder Named(string name) => With(c => c with { Name = name });

    public PlatformChannelBuilder OfType(PlatformChannelType type) => With(c => c with { Type = type });

    public PlatformChannelBuilder InCategory(PlatformRef category) => With(c => c with { Category = category });

    public PlatformChannelBuilder AsThreadOf(PlatformRef parent) =>
        With(c => c with { Type = PlatformChannelType.Thread, Category = null, ThreadParent = parent });

    protected override PlatformChannel CreateDefault() => new(
        Channel: TestPlatform.Channel("general"),
        Name: "general",
        Type: PlatformChannelType.Text,
        Category: null,
        ThreadParent: null);
}
