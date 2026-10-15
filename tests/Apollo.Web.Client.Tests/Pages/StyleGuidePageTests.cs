using Apollo.Web.Client.Components;
using Apollo.Web.Client.Pages;
using Apollo.Web.Client.Theme;
using Bunit;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Pages;

public sealed class StyleGuidePageTests : ApolloTestContext
{
    [Fact]
    public void StyleGuidePage_ShouldRenderWithoutErrors()
    {
        Render<MudPopoverProvider>();

        var page = Render<StyleGuide>();

        page.FindComponents<StatTile>().ShouldNotBeEmpty();
        page.FindComponents<SectionCard>().ShouldNotBeEmpty();
        page.FindComponents<EmptyState>().ShouldNotBeEmpty();
        page.FindAll(".apollo-swatch").Count.ShouldBe(ApolloColors.All.Count);
    }

    [Fact]
    public void StyleGuidePage_ShouldNumberChartColoursInSeriesOrder()
    {
        Render<MudPopoverProvider>();

        var page = Render<StyleGuide>();

        var labels = page.FindAll(".apollo-chart-swatch").Select(s => s.TextContent.Trim());
        labels.ShouldBe(ChartPalette.Series.Select((colour, i) => $"{i + 1}. {colour}"));
    }
}
