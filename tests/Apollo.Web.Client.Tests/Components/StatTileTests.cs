using Apollo.Web.Client.Components;
using Bunit;

namespace Apollo.Web.Client.Tests.Components;

public sealed class StatTileTests : ApolloTestContext
{
    [Fact]
    public void StatTile_WhenValueIsLarge_ShouldFormatWithSwissGrouping()
    {
        var tile = Render<StatTile>(p => p
            .Add(t => t.Label, "Nachrichten")
            .Add(t => t.Value, 1234567));

        tile.Find(".apollo-stat-value").TextContent.Trim().ShouldBe("1'234'567");
    }

    [Fact]
    public void StatTile_WhenDecimalsAreSet_ShouldUseSwissDecimalPointAndUnit()
    {
        var tile = Render<StatTile>(p => p
            .Add(t => t.Label, "Voice-Stunden")
            .Add(t => t.Value, 1234.56)
            .Add(t => t.Decimals, 1)
            .Add(t => t.Unit, "h"));

        tile.Find(".apollo-stat-value").TextContent.Trim().ShouldBe("1'234.6 h");
    }

    [Fact]
    public void StatTile_WhenValueMissing_ShouldShowPlaceholder()
    {
        var tile = Render<StatTile>(p => p
            .Add(t => t.Label, "Nachrichten")
            .Add(t => t.Value, null)
            .Add(t => t.Unit, "h"));

        tile.Find(".apollo-stat-value").TextContent.Trim().ShouldBe("–");
    }

    [Fact]
    public void StatTile_ShouldRenderLabelAndSubText()
    {
        var tile = Render<StatTile>(p => p
            .Add(t => t.Label, "Mitglieder")
            .Add(t => t.Value, 42)
            .Add(t => t.SubText, "davon 30 erfasst"));

        tile.Find(".apollo-stat-label").TextContent.Trim().ShouldBe("Mitglieder");
        tile.Find(".apollo-stat-subtext").TextContent.Trim().ShouldBe("davon 30 erfasst");
    }

    [Fact]
    public void StatTile_WhenSubTextAndDeltaMissing_ShouldNotRenderThem()
    {
        var tile = Render<StatTile>(p => p
            .Add(t => t.Label, "Mitglieder")
            .Add(t => t.Value, 42));

        tile.FindAll(".apollo-stat-subtext").ShouldBeEmpty();
        tile.FindAll(".apollo-stat-delta").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(12.5, "+12.5 %", "mud-success-text")]
    [InlineData(-3.0, "-3 %", "mud-error-text")]
    [InlineData(0.0, "±0 %", "mud-secondary-text")]
    public void StatTile_WhenDeltaIsSet_ShouldShowSignedPercentWithTrendColour(double delta, string expectedText, string expectedClass)
    {
        var tile = Render<StatTile>(p => p
            .Add(t => t.Label, "Nachrichten")
            .Add(t => t.Value, 100)
            .Add(t => t.Delta, delta));

        var deltaElement = tile.Find(".apollo-stat-delta");
        deltaElement.TextContent.Trim().ShouldBe(expectedText);
        deltaElement.ClassList.ShouldContain(expectedClass);
    }
}
