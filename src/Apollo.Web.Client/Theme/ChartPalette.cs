namespace Apollo.Web.Client.Theme;

/// <summary>
/// Chart colours for the dark theme. The series order follows the Okabe-Ito palette (safe for the common
/// colour-vision deficiencies), lightened where needed to reach at least 3:1 against <see cref="ApolloColors.Surface"/>.
/// </summary>
public static class ChartPalette
{
    /// <summary>Series colours in the order they are assigned. Charts with more series should group the rest as "Andere".</summary>
    public static IReadOnlyList<string> Series { get; } =
    [
        ApolloColors.PrimaryLight, // blurple
        "#E69F00", // orange
        "#56B4E9", // sky blue
        "#2EC4A0", // bluish green
        "#F0E442", // yellow
        "#FF7A45", // vermilion
        "#D88BC0", // reddish purple
        "#C3C8D1", // light grey
    ];

    /// <summary>Single-hue sequential scale for heatmaps, from "no activity" to "most activity".</summary>
    public static IReadOnlyList<string> HeatmapScale { get; } =
        ["#2F3138", "#353A6B", "#3F48A3", "#5560D6", ApolloColors.PrimaryLight, "#C5C9FF"];

    public const string Background = ApolloColors.Surface;
    public const string Grid = ApolloColors.Divider;
    public const string Axis = ApolloColors.Line;
    public const string Label = ApolloColors.TextSecondary;
}
