using Apollo.Web.Client.Theme;
using MudBlazor;
using MudBlazor.Utilities;

namespace Apollo.Web.Client.Tests.Theme;

public sealed class ApolloThemeTests
{
    // Every palette slot read by the MudBlazor components on the style guide page.
    private static readonly string[] _explicitSlots =
    [
        nameof(Palette.Primary), nameof(Palette.PrimaryContrastText),
        nameof(Palette.Secondary), nameof(Palette.SecondaryContrastText),
        nameof(Palette.Tertiary), nameof(Palette.TertiaryContrastText),
        nameof(Palette.Info), nameof(Palette.InfoContrastText),
        nameof(Palette.Success), nameof(Palette.SuccessContrastText),
        nameof(Palette.Warning), nameof(Palette.WarningContrastText),
        nameof(Palette.Error), nameof(Palette.ErrorContrastText),
        nameof(Palette.Dark), nameof(Palette.DarkContrastText),
        nameof(Palette.Background), nameof(Palette.BackgroundGray), nameof(Palette.Surface),
        nameof(Palette.DrawerBackground), nameof(Palette.DrawerText), nameof(Palette.DrawerIcon),
        nameof(Palette.AppbarBackground), nameof(Palette.AppbarText),
        nameof(Palette.TextPrimary), nameof(Palette.TextSecondary), nameof(Palette.TextDisabled),
        nameof(Palette.ActionDefault), nameof(Palette.ActionDisabled), nameof(Palette.ActionDisabledBackground),
        nameof(Palette.Divider), nameof(Palette.DividerLight),
        nameof(Palette.LinesDefault), nameof(Palette.LinesInputs),
        nameof(Palette.TableLines), nameof(Palette.TableStriped), nameof(Palette.TableHover),
        nameof(Palette.Skeleton),
    ];

    public static TheoryData<string> ExplicitSlots() => [.. _explicitSlots];

    [Theory]
    [MemberData(nameof(ExplicitSlots))]
    public void ApolloTheme_DarkPalette_ShouldTakeSlotFromAnApolloToken(string slot)
    {
        var tokenColours = ApolloColors.All.Select(t => new MudColor(t.Hex).ToString()).ToHashSet();
        var property = typeof(Palette).GetProperty(slot)!;

        var apollo = property.GetValue(ApolloTheme.Create().PaletteDark)!.ToString();

        tokenColours.ShouldContain(apollo!, $"{slot} is not an Apollo colour token (MudBlazor default bleeding through?)");
    }

    [Theory]
    [InlineData(ApolloColors.Surface)]
    [InlineData(ApolloColors.SurfaceRaised)]
    [InlineData(ApolloColors.Background)]
    [InlineData(ApolloColors.Drawer)]
    public void ApolloTheme_TextPrimary_ShouldMeetWcagAaOn(string background)
    {
        var ratio = WcagContrast.Ratio(ApolloColors.TextPrimary, background);

        ratio.ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [InlineData(ApolloColors.Surface)]
    [InlineData(ApolloColors.SurfaceRaised)]
    [InlineData(ApolloColors.Background)]
    public void ApolloTheme_TextSecondary_ShouldMeetWcagAaOn(string background)
    {
        var ratio = WcagContrast.Ratio(ApolloColors.TextSecondary, background);

        ratio.ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [InlineData(ApolloColors.Primary, ApolloColors.OnPrimary)]
    [InlineData(ApolloColors.Secondary, ApolloColors.OnStatus)]
    [InlineData(ApolloColors.Success, ApolloColors.OnStatus)]
    [InlineData(ApolloColors.Warning, ApolloColors.OnStatus)]
    [InlineData(ApolloColors.Error, ApolloColors.OnStatus)]
    [InlineData(ApolloColors.Info, ApolloColors.OnStatus)]
    public void ApolloTheme_FilledButtonText_ShouldMeetWcagAa(string fill, string text)
    {
        var ratio = WcagContrast.Ratio(text, fill);

        ratio.ShouldBeGreaterThanOrEqualTo(4.5);
    }

    [Theory]
    [InlineData(ApolloColors.Primary)]
    [InlineData(ApolloColors.Secondary)]
    [InlineData(ApolloColors.Success)]
    [InlineData(ApolloColors.Warning)]
    [InlineData(ApolloColors.Error)]
    [InlineData(ApolloColors.Info)]
    public void ApolloTheme_AccentOnSurface_ShouldMeetWcagNonTextContrast(string accent)
    {
        var ratio = WcagContrast.Ratio(accent, ApolloColors.Surface);

        ratio.ShouldBeGreaterThanOrEqualTo(3.0);
    }

    [Fact]
    public void ApolloTheme_ShouldUseSystemFontStackForAllTypography()
    {
        var typography = ApolloTheme.Create().Typography;

        typography.Default.FontFamily.ShouldBe(ApolloTheme.FontFamily);
        typography.H1.FontFamily.ShouldBe(ApolloTheme.FontFamily);
        typography.Body1.FontFamily.ShouldBe(ApolloTheme.FontFamily);
    }

    [Fact]
    public void ApolloColors_All_ShouldListEveryTokenOnce()
    {
        ApolloColors.All.Select(t => t.Name).Distinct().Count().ShouldBe(ApolloColors.All.Count);
        ApolloColors.All.ShouldContain(t => t.Hex == ApolloColors.Primary);
    }
}
