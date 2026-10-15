namespace Apollo.Web.Client.Layout;

/// <summary>A group of entries in the navigation drawer, with an optional heading.</summary>
/// <param name="TitleKey">Key of the heading in <c>Strings.resx</c>, or <c>null</c> for no heading.</param>
/// <param name="Items">The entries, in display order.</param>
public sealed record NavigationSection(string? TitleKey, IReadOnlyList<NavigationItem> Items);
