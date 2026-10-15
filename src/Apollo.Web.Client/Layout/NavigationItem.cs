namespace Apollo.Web.Client.Layout;

/// <summary>One entry in the navigation drawer.</summary>
/// <param name="Href">Base-relative route, e.g. <c>admin/bot-status</c>; empty for the home page.</param>
/// <param name="LabelKey">Key of the label in <c>Strings.resx</c>.</param>
/// <param name="Icon">MudBlazor icon.</param>
public sealed record NavigationItem(string Href, string LabelKey, string Icon);
