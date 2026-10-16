using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="User"/> row with its snapshot columns. <see cref="SeedAsync"/> creates the internal ID through
/// <see cref="IPlatformIdResolver"/> (the user defaults to <c>TestPlatform.User("alice")</c>) and stores the columns.
/// </summary>
public sealed class UserBuilder : TestDataBuilder<UserBuilder, User>
{
    private PlatformRef _user = TestPlatform.User("alice");

    public UserBuilder For(PlatformRef user)
    {
        _user = user;
        return this;
    }

    public UserBuilder Named(string displayName) => With(u =>
    {
        u.DisplayName = displayName;
        return u;
    });

    public UserBuilder WithAvatar(string? avatarUrl) => With(u =>
    {
        u.AvatarUrl = avatarUrl;
        return u;
    });

    public async Task<User> SeedAsync(ApolloDbContext db, IPlatformIdResolver ids, CancellationToken cancellationToken)
    {
        var id = await ids.ResolveUserAsync(_user, cancellationToken);
        var row = await db.Users.SingleAsync(u => u.Id == id, cancellationToken);
        var values = Build();
        row.DisplayName = values.DisplayName;
        row.AvatarUrl = values.AvatarUrl;
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    protected override User CreateDefault() => new() { DisplayName = "Alice" };
}
