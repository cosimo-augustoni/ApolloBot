using MudBlazor;

namespace Apollo.Web.Client.Layout;

/// <summary>The areas of the app (scope §7.2), as shown in the navigation drawer.</summary>
public static class NavigationAreas
{
    public static IReadOnlyList<NavigationSection> Sections { get; } =
    [
        new(null,
        [
            new("", "Nav_Home", Icons.Material.Outlined.Home),
        ]),
        new("Nav_SectionAdmin",
        [
            new("admin/bot-status", "Nav_BotStatus", Icons.Material.Outlined.SmartToy),
            new("admin/server", "Nav_Server", Icons.Material.Outlined.Dns),
            new("admin/members", "Nav_Members", Icons.Material.Outlined.People),
            new("admin/recaps", "Nav_AdminRecaps", Icons.Material.Outlined.AutoAwesome),
            new("admin/data", "Nav_Data", Icons.Material.Outlined.Storage),
            new("admin/settings", "Nav_Settings", Icons.Material.Outlined.Settings),
        ]),
        new("Nav_SectionMember",
        [
            new("me", "Nav_Overview", Icons.Material.Outlined.Insights),
            new("me/recaps", "Nav_MemberRecaps", Icons.Material.Outlined.AutoAwesome),
            new("me/privacy", "Nav_Privacy", Icons.Material.Outlined.Shield),
        ]),
    ];

    public static IEnumerable<NavigationItem> Items => Sections.SelectMany(s => s.Items);

    /// <summary>The entry for a base-relative path (query and fragment ignored), or <c>null</c>.</summary>
    public static NavigationItem? Find(string relativePath)
    {
        var path = relativePath.Split('?', '#')[0].Trim('/');
        return Items.FirstOrDefault(i => string.Equals(i.Href, path, StringComparison.OrdinalIgnoreCase));
    }
}
