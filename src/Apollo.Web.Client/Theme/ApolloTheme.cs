using MudBlazor;

namespace Apollo.Web.Client.Theme;

/// <summary>
/// The dark-first MudBlazor theme (scope §7.1.1): dark grey surfaces, an own blurple-like accent,
/// rounded corners and a dense type scale. Colours come from <see cref="ApolloColors"/>.
/// </summary>
public static class ApolloTheme
{
    /// <summary>System font stack: no web font download, no third-party CDN.</summary>
    public static string[] FontFamily { get; } =
        ["system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "Noto Sans", "sans-serif"];

    /// <summary><see cref="FontFamily"/> as a CSS <c>font-family</c> value, for places outside MudBlazor (charts).</summary>
    public static string FontFamilyCss { get; } =
        string.Join(", ", FontFamily.Select(f => f.Contains(' ') ? $"\"{f}\"" : f));

    /// <summary>Corner radius of cards, buttons and inputs.</summary>
    public const string BorderRadius = "8px";

    public static MudTheme Create() => new()
    {
        PaletteDark = CreateDarkPalette(),
        Typography = CreateTypography(),
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = BorderRadius,
            AppbarHeight = "56px",
            DrawerWidthLeft = "260px",
        },
    };

    private static PaletteDark CreateDarkPalette() => new()
    {
        Primary = ApolloColors.Primary,
        PrimaryContrastText = ApolloColors.OnPrimary,
        Secondary = ApolloColors.Secondary,
        SecondaryContrastText = ApolloColors.OnStatus,
        Tertiary = ApolloColors.Tertiary,
        TertiaryContrastText = ApolloColors.OnStatus,
        Info = ApolloColors.Info,
        InfoContrastText = ApolloColors.OnStatus,
        Success = ApolloColors.Success,
        SuccessContrastText = ApolloColors.OnStatus,
        Warning = ApolloColors.Warning,
        WarningContrastText = ApolloColors.OnStatus,
        Error = ApolloColors.Error,
        ErrorContrastText = ApolloColors.OnStatus,
        Dark = ApolloColors.Drawer,
        DarkContrastText = ApolloColors.TextPrimary,

        Background = ApolloColors.Background,
        BackgroundGray = ApolloColors.Drawer,
        Surface = ApolloColors.Surface,
        DrawerBackground = ApolloColors.Drawer,
        DrawerText = ApolloColors.TextSecondary,
        DrawerIcon = ApolloColors.TextSecondary,
        AppbarBackground = ApolloColors.AppBar,
        AppbarText = ApolloColors.TextPrimary,

        TextPrimary = ApolloColors.TextPrimary,
        TextSecondary = ApolloColors.TextSecondary,
        TextDisabled = ApolloColors.TextDisabled,
        ActionDefault = ApolloColors.TextSecondary,
        ActionDisabled = ApolloColors.TextDisabled,
        ActionDisabledBackground = ApolloColors.SurfaceRaised,

        Divider = ApolloColors.Divider,
        DividerLight = ApolloColors.Line,
        LinesDefault = ApolloColors.Line,
        LinesInputs = ApolloColors.TextDisabled,
        TableLines = ApolloColors.Divider,
        TableStriped = ApolloColors.SurfaceRaised,
        TableHover = ApolloColors.SurfaceRaised,
        Skeleton = ApolloColors.SurfaceRaised,
    };

    // Dense but readable: body text at 15px, headings kept compact so dashboards fit on one screen.
    private static Typography CreateTypography() => new()
    {
        Default = new DefaultTypography { FontFamily = FontFamily, FontSize = "0.9375rem", LineHeight = "1.5" },
        H1 = new H1Typography { FontFamily = FontFamily, FontSize = "2.25rem", FontWeight = "700", LineHeight = "1.2" },
        H2 = new H2Typography { FontFamily = FontFamily, FontSize = "1.875rem", FontWeight = "700", LineHeight = "1.25" },
        H3 = new H3Typography { FontFamily = FontFamily, FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.3" },
        H4 = new H4Typography { FontFamily = FontFamily, FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.35" },
        H5 = new H5Typography { FontFamily = FontFamily, FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.4" },
        H6 = new H6Typography { FontFamily = FontFamily, FontSize = "1rem", FontWeight = "600", LineHeight = "1.4" },
        Subtitle1 = new Subtitle1Typography { FontFamily = FontFamily, FontSize = "1rem", FontWeight = "500", LineHeight = "1.5" },
        Subtitle2 = new Subtitle2Typography { FontFamily = FontFamily, FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.45" },
        Body1 = new Body1Typography { FontFamily = FontFamily, FontSize = "0.9375rem", FontWeight = "400", LineHeight = "1.5" },
        Body2 = new Body2Typography { FontFamily = FontFamily, FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.45" },
        Button = new ButtonTypography { FontFamily = FontFamily, FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.75", TextTransform = "none" },
        Caption = new CaptionTypography { FontFamily = FontFamily, FontSize = "0.75rem", FontWeight = "400", LineHeight = "1.4" },
        Overline = new OverlineTypography { FontFamily = FontFamily, FontSize = "0.6875rem", FontWeight = "700", LineHeight = "1.6", LetterSpacing = "0.06em", TextTransform = "uppercase" },
    };
}
