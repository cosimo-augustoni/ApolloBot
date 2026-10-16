using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The synced guild state read back from the database, keyed by external platform ID so tests can assert in the
/// words of their snapshot (<c>"alice"</c>, <c>"statistik"</c>). One guild per test.
/// </summary>
internal sealed record GuildStateView(
    GuildStateView.GuildRow? Guild,
    IReadOnlyDictionary<string, GuildStateView.RoleRow> Roles,
    IReadOnlyDictionary<string, GuildStateView.ChannelRow> Channels,
    IReadOnlyDictionary<string, GuildStateView.UserRow> Users,
    IReadOnlyDictionary<string, GuildStateView.MemberRow> Members,
    IReadOnlyList<(string User, string Role)> MemberRoles)
{
    /// <summary>
    /// The roles of <paramref name="user"/>, by external ID.
    /// </summary>
    public IReadOnlyList<string> RolesOf(string user) => [.. MemberRoles.Where(r => r.User == user).Select(r => r.Role).Order()];

    /// <summary>
    /// Every row as one line each, sorted, to compare two states as a whole.
    /// </summary>
    public IReadOnlyList<string> Lines() =>
    [
        $"guild {Guild}",
        .. Roles.Select(r => $"role {r.Key} {r.Value}"),
        .. Channels.Select(c => $"channel {c.Key} {c.Value}"),
        .. Users.Select(u => $"user {u.Key} {u.Value}"),
        .. Members.Select(m => $"member {m.Key} {m.Value}"),
        .. MemberRoles.Select(r => $"member_role {r.User} {r.Role}"),
    ];

    public static async Task<GuildStateView> LoadAsync(TimescaleFixture fixture)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CreateDbContext();

        var users = await db.Users
            .Join(db.UserPlatformIds, u => u.Id, m => m.UserId, (u, m) => new { u.Id, m.ExternalId, u.DisplayName, u.AvatarUrl })
            .ToListAsync(ct);
        var userIds = users.ToDictionary(u => u.Id, u => u.ExternalId);

        var channels = await db.Channels
            .Join(db.ChannelPlatformIds, c => c.Id, m => m.ChannelId, (c, m) => new { c.Id, m.ExternalId, c.Name, c.Type, c.CategoryId, c.ThreadParentId, c.DeletedAt })
            .ToListAsync(ct);
        var channelIds = channels.ToDictionary(c => c.Id, c => c.ExternalId);

        var roles = await db.Roles
            .Join(db.RolePlatformIds, r => r.Id, m => m.RoleId, (r, m) => new { r.Id, m.ExternalId, r.Name, r.Color, r.Position, r.IsAdministrator, r.DeletedAt })
            .ToListAsync(ct);
        var roleIds = roles.ToDictionary(r => r.Id, r => r.ExternalId);

        var guild = await db.Guilds.SingleOrDefaultAsync(ct);
        var members = await db.GuildMembers.ToListAsync(ct);
        var memberRoles = await db.MemberRoles.ToListAsync(ct);

        return new GuildStateView(
            guild is null ? null : new GuildRow(guild.Name, guild.OwnerUserId is { } owner ? userIds[owner] : null),
            roles.OrderBy(r => r.ExternalId).ToDictionary(r => r.ExternalId, r => new RoleRow(r.Name, r.Color, r.Position, r.IsAdministrator, r.DeletedAt)),
            channels.OrderBy(c => c.ExternalId).ToDictionary(c => c.ExternalId, c => new ChannelRow(
                c.Name,
                c.Type,
                c.CategoryId is { } category ? channelIds[category] : null,
                c.ThreadParentId is { } parent ? channelIds[parent] : null,
                c.DeletedAt)),
            users.OrderBy(u => u.ExternalId).ToDictionary(u => u.ExternalId, u => new UserRow(u.DisplayName, u.AvatarUrl)),
            members.OrderBy(m => userIds[m.UserId]).ToDictionary(m => userIds[m.UserId], m => new MemberRow(m.JoinedAt, m.IsAdministrator, m.IsBot, m.LeftAt)),
            [.. memberRoles.Select(r => (userIds[r.UserId], roleIds[r.RoleId])).Order()]);
    }

    internal sealed record GuildRow(string? Name, string? Owner);

    internal sealed record RoleRow(string? Name, int? Color, int Position, bool IsAdministrator, DateTimeOffset? DeletedAt);

    internal sealed record ChannelRow(string? Name, PlatformChannelType? Type, string? Category, string? ThreadParent, DateTimeOffset? DeletedAt);

    internal sealed record UserRow(string? DisplayName, string? AvatarUrl);

    internal sealed record MemberRow(DateTimeOffset? JoinedAt, bool IsAdministrator, bool IsBot, DateTimeOffset? LeftAt);
}
