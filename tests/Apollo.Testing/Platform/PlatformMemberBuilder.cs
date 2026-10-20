using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformMemberBuilder : TestDataBuilder<PlatformMemberBuilder, PlatformMember>
{
    public PlatformMemberBuilder ForUser(PlatformRef user) => With(m => m with { User = user });

    public PlatformMemberBuilder Named(string displayName) => With(m => m with { DisplayName = displayName });

    public PlatformMemberBuilder WithNickname(string? nickname) => With(m => m with { Nickname = nickname });

    public PlatformMemberBuilder WithGlobalName(string? globalName) => With(m => m with { GlobalName = globalName });

    public PlatformMemberBuilder WithUsername(string username) => With(m => m with { Username = username });

    public PlatformMemberBuilder WithAvatar(string? avatarUrl) => With(m => m with { AvatarUrl = avatarUrl });

    public PlatformMemberBuilder JoinedAt(DateTimeOffset? joinedAt) => With(m => m with { JoinedAt = joinedAt });

    public PlatformMemberBuilder WithRoles(params PlatformRef[] roles) => With(m => m with { Roles = roles });

    public PlatformMemberBuilder AsAdministrator() => With(m => m with { IsAdministrator = true });

    public PlatformMemberBuilder AsBot() => With(m => m with { IsBot = true });

    protected override PlatformMember CreateDefault() => new(
        User: TestPlatform.User("alice"),
        DisplayName: "Alice",
        Nickname: null,
        GlobalName: "Alice",
        Username: "alice",
        AvatarUrl: null,
        IsBot: false,
        IsAdministrator: false,
        JoinedAt: TestTime.Now.AddDays(-30),
        Roles: []);
}
