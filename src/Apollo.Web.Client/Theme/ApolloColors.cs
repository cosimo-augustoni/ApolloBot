namespace Apollo.Web.Client.Theme;

/// <summary>
/// Colour tokens of the dark Apollo theme. <see cref="ApolloTheme"/>, <see cref="ChartPalette"/> and the
/// style guide page all read from here; docs/design/style-guide.md lists the same values with contrast ratios.
/// </summary>
public static class ApolloColors
{
    // Surfaces, darkest to lightest.
    public const string Drawer = "#18191D";
    public const string Background = "#1E1F24";
    public const string AppBar = "#222328";
    public const string Surface = "#2A2C32";
    public const string SurfaceRaised = "#33363D";
    public const string Line = "#3D4048";
    public const string Divider = "#393C44";

    // Text.
    public const string TextPrimary = "#F1F2F4";
    public const string TextSecondary = "#B3B8C2";
    public const string TextDisabled = "#6E737D";

    // Accents and status colours.
    public const string Primary = "#5D66E8";
    public const string PrimaryLight = "#8A91FF";
    public const string Secondary = "#2FB8A6";
    public const string Tertiary = "#D96FB4";
    public const string Success = "#3DB86E";
    public const string Warning = "#F0B23E";
    public const string Error = "#F26B70";
    public const string Info = "#4BA3F0";

    // Text on filled accents.
    public const string OnPrimary = "#FFFFFF";
    public const string OnStatus = "#0F1114";

    /// <summary>Every token with its usage, in the order the style guide shows them.</summary>
    public static IReadOnlyList<ColorToken> All { get; } =
    [
        new(nameof(Drawer), Drawer, "Navigation, dunkelste Ebene"),
        new(nameof(Background), Background, "Seitenhintergrund"),
        new(nameof(AppBar), AppBar, "App-Leiste"),
        new(nameof(Surface), Surface, "Karten, Diagrammhintergrund"),
        new(nameof(SurfaceRaised), SurfaceRaised, "Hover, Menüs, Tabellenstreifen"),
        new(nameof(Divider), Divider, "Trennlinien, Diagrammraster"),
        new(nameof(Line), Line, "Rahmen, Diagrammachsen"),
        new(nameof(TextPrimary), TextPrimary, "Fliesstext, Überschriften"),
        new(nameof(TextSecondary), TextSecondary, "Beschriftungen, Hinweise"),
        new(nameof(TextDisabled), TextDisabled, "Deaktiviert, Eingaberahmen"),
        new(nameof(Primary), Primary, "Akzent: Hauptaktionen, aktiver Zustand"),
        new(nameof(PrimaryLight), PrimaryLight, "Akzent hell: erste Datenreihe, Links"),
        new(nameof(Secondary), Secondary, "Nebenaktionen"),
        new(nameof(Tertiary), Tertiary, "Hervorhebungen in Rückblicken"),
        new(nameof(Success), Success, "Erfasst, positiver Trend"),
        new(nameof(Warning), Warning, "Keine Tracking-Rolle, Achtung"),
        new(nameof(Error), Error, "Fehler, negativer Trend"),
        new(nameof(Info), Info, "Hinweise"),
        new(nameof(OnPrimary), OnPrimary, "Text auf Primary"),
        new(nameof(OnStatus), OnStatus, "Text auf Secondary und Statusfarben"),
    ];
}
