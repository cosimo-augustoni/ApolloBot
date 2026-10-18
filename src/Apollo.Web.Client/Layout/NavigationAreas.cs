using MudBlazor;

namespace Apollo.Web.Client.Layout;

/// <summary>
/// The areas of the app (scope §7.2), as shown in the navigation drawer. Routes, labels and grouping follow the
/// wireframes in <c>docs/design/wireframes</c> (#27); "Datenprüfung" (#40) has no wireframe and sits after "Daten".
/// </summary>
public static class NavigationAreas
{
    public static IReadOnlyList<NavigationSection> Sections { get; } =
    [
        new("Nav_SectionServer",
        [
            new("admin", "Nav_BotStatus", "Nav_BotStatus", Icons.Material.Outlined.SmartToy),
            new("admin/dashboard", "Nav_ServerOverview", "Page_ServerOverview", Icons.Material.Outlined.Dashboard),
            new("admin/members", "Nav_Members", "Nav_Members", Icons.Material.Outlined.People),
            new("admin/recaps", "Nav_ServerRecaps", "Page_ServerRecaps", Icons.Material.Outlined.AutoAwesome),
            new("admin/data", "Nav_Data", "Nav_Data", Icons.Material.Outlined.Storage),
            new("admin/sanity", "Nav_Sanity", "Nav_Sanity", Icons.Material.Outlined.FactCheck),
            new("admin/settings", "Nav_Settings", "Nav_Settings", Icons.Material.Outlined.Settings),
        ]),
        new("Nav_SectionMember",
        [
            new("me", "Nav_MemberOverview", "Page_MemberOverview", Icons.Material.Outlined.Insights),
        ]),
        new("Nav_SectionInfo",
        [
            new("datenschutz", "Nav_PrivacyPolicy", "Nav_PrivacyPolicy", Icons.Material.Outlined.Policy),
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
