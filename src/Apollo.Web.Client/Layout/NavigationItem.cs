namespace Apollo.Web.Client.Layout;

/// <summary>One entry in the navigation drawer.</summary>
/// <param name="Href">Base-relative route, e.g. <c>admin/members</c>.</param>
/// <param name="LabelKey">Key of the drawer label in <c>Strings.resx</c>.</param>
/// <param name="TitleKey">Key of the page heading in <c>Strings.resx</c> (can be more specific than the label).</param>
/// <param name="Icon">MudBlazor icon.</param>
public sealed record NavigationItem(string Href, string LabelKey, string TitleKey, string Icon);
