using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformPresenceBuilder : TestDataBuilder<PlatformPresenceBuilder, PlatformPresence>
{
    public PlatformPresenceBuilder ForUser(PlatformRef user) => With(p => p with { User = user });

    public PlatformPresenceBuilder WithStatus(PlatformPresenceStatus status) => With(p => p with { Status = status });

    public PlatformPresenceBuilder Playing(string game, DateTimeOffset? startedAt = null) =>
        With(p => p with { Activities = [.. p.Activities, new PlatformActivity(PlatformActivityType.Playing, game, startedAt)] });

    protected override PlatformPresence CreateDefault() => new(
        User: TestPlatform.User("alice"),
        Status: PlatformPresenceStatus.Online,
        Activities: []);
}
