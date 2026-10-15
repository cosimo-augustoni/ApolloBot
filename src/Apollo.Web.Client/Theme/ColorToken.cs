namespace Apollo.Web.Client.Theme;

/// <summary>A named colour of the Apollo design system, as shown on the style guide page.</summary>
/// <param name="Name">Token name, e.g. <c>Surface</c>.</param>
/// <param name="Hex">Colour as <c>#RRGGBB</c>.</param>
/// <param name="Usage">Where the colour is used; German, because the style guide page shows it.</param>
public sealed record ColorToken(string Name, string Hex, string Usage);
