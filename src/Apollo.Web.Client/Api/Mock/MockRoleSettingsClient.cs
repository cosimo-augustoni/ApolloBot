using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api.Mock;

/// <summary>
/// <see cref="IRoleSettingsClient"/> without a host, for the prototype and bUnit tests: one Discord guild in bootstrap
/// mode with roles like the wireframe's. Writes change the in-memory state and add history entries; the preview adds up
/// role member counts (the real host counts distinct members). Tests set <see cref="Platforms"/>,
/// <see cref="Settings"/> and <see cref="History"/> before rendering.
/// </summary>
public sealed class MockRoleSettingsClient(TimeProvider time) : IRoleSettingsClient
{
    public static readonly Guid Statistik = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Mitglied = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    public static readonly Guid Stammgast = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    public static readonly Guid Moderation = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    public static readonly Guid Admin = Guid.Parse("00000000-0000-0000-0000-0000000000a5");
    public static readonly Guid Alt = Guid.Parse("00000000-0000-0000-0000-0000000000a6");

    /// <summary>The members tracked in bootstrap mode (the Administrators).</summary>
    public const int Administrators = 3;

    public List<string> Platforms { get; set; } = ["Discord"];

    /// <summary>The settings of every platform in <see cref="Platforms"/>.</summary>
    public RoleSettingsDto Settings { get; set; } = new(
        "Discord",
        [
            new SettingsRoleDto(Admin, "Admin", 0xE74C3C, 7, IsDeleted: false, MemberCount: Administrators),
            new SettingsRoleDto(Moderation, "Moderation", 0x9B59B6, 6, IsDeleted: false, MemberCount: 5),
            new SettingsRoleDto(Statistik, "Statistik", 0x3498DB, 5, IsDeleted: false, MemberCount: 168),
            new SettingsRoleDto(Mitglied, "Mitglied", 0x2ECC71, 4, IsDeleted: false, MemberCount: 187),
            new SettingsRoleDto(Stammgast, "Stammgast", 0xE67E22, 3, IsDeleted: false, MemberCount: 40),
            new SettingsRoleDto(Alt, "Alt", null, 1, IsDeleted: true, MemberCount: 0),
        ],
        TrackingRoleId: null,
        MemberAccessRoleIds: [Mitglied, Stammgast],
        RoleSettingsMode.Bootstrap,
        new BotPermissionCheckDto(CanManageRoles: true, IsAboveTrackingRole: null));

    /// <summary>The change history, newest first.</summary>
    public List<RoleSettingChangeDto> History { get; set; } =
    [
        new(new DateTimeOffset(2026, 10, 2, 18, 14, 0, TimeSpan.Zero), "Anna", RoleSettingKind.MemberAccess, RoleSettingChangeAction.Added, "Stammgast"),
        new(new DateTimeOffset(2026, 9, 1, 16, 2, 0, TimeSpan.Zero), "Anna", RoleSettingKind.MemberAccess, RoleSettingChangeAction.Added, "Mitglied"),
    ];

    public Task<IReadOnlyList<string>> GetPlatformsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. Platforms]);

    public Task<RoleSettingsDto> GetSettingsAsync(string platform, CancellationToken cancellationToken) =>
        Task.FromResult(Settings with { Platform = platform });

    public Task<RolePreviewDto> GetPreviewAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var members = Settings.Roles.Where(r => !r.IsDeleted && roleIds.Contains(r.Id)).Sum(r => r.MemberCount);
        var trackedNow = Settings.Mode switch
        {
            RoleSettingsMode.Bootstrap => Administrators,
            RoleSettingsMode.Active => Role(Settings.TrackingRoleId!.Value).MemberCount,
            _ => 0,
        };
        var unchanged = Settings.Mode == RoleSettingsMode.Active && roleIds.SequenceEqual([Settings.TrackingRoleId!.Value]);
        return Task.FromResult(unchanged ? new RolePreviewDto(members, 0, 0) : new RolePreviewDto(members, members, trackedNow));
    }

    public Task<IReadOnlyList<RoleSettingChangeDto>> GetHistoryAsync(string platform, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoleSettingChangeDto>>([.. History.Take(take)]);

    public Task SetTrackingRoleAsync(string platform, Guid? roleId, CancellationToken cancellationToken)
    {
        var changes = new List<RoleSettingChangeDto>();
        if (Settings.TrackingRoleId is { } current && current != roleId)
        {
            changes.Add(Change(RoleSettingKind.Tracking, RoleSettingChangeAction.Removed, current));
        }

        if (roleId is { } added && added != Settings.TrackingRoleId)
        {
            changes.Add(Change(RoleSettingKind.Tracking, RoleSettingChangeAction.Added, added));
        }

        Settings = Settings with
        {
            TrackingRoleId = roleId,
            Mode = roleId is null ? RoleSettingsMode.TrackingRoleCleared : RoleSettingsMode.Active,
            BotPermissions = Settings.BotPermissions with { IsAboveTrackingRole = roleId is null ? null : true },
        };
        AddHistory(changes);
        return Task.CompletedTask;
    }

    public Task SetMemberAccessRolesAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken)
    {
        var current = Settings.MemberAccessRoleIds;
        AddHistory(
        [
            .. current.Except(roleIds).Select(id => Change(RoleSettingKind.MemberAccess, RoleSettingChangeAction.Removed, id)),
            .. roleIds.Except(current).Select(id => Change(RoleSettingKind.MemberAccess, RoleSettingChangeAction.Added, id)),
        ]);
        Settings = Settings with { MemberAccessRoleIds = [.. roleIds.Distinct()] };
        return Task.CompletedTask;
    }

    private SettingsRoleDto Role(Guid id) => Settings.Roles.Single(r => r.Id == id);

    private RoleSettingChangeDto Change(RoleSettingKind setting, RoleSettingChangeAction action, Guid roleId) =>
        new(time.GetUtcNow(), MockAuthClient.User.DisplayName, setting, action, Role(roleId).Name);

    // Newest first: the last change of one write is the newest.
    private void AddHistory(IEnumerable<RoleSettingChangeDto> changes) => History.InsertRange(0, changes.Reverse());
}
