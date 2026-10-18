using Apollo.Web.Client.Components.Stats;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>The range picker of the stats pages (#37): presets "Monat", "Jahr" and "Benutzerdefiniert".</summary>
public sealed class DateRangeSelectorTests : ApolloTestContext
{
    // 18 October 2026, 14:00 in Zurich.
    private static readonly DateOnly _today = new(2026, 10, 18);

    private readonly List<DateRangeSelection> _changes = [];

    public DateRangeSelectorTests() =>
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public void DateRangeSelector_ShouldShowThePresetAndTheRange()
    {
        var selector = RenderSelector(new DateRangeSelection(DateRangePreset.Month, new DateOnly(2026, 10, 1), _today));

        selector.Find(".apollo-range-text").TextContent.Trim().ShouldBe("01.10.2026 – 18.10.2026");
        Preset(selector, "Monat").ClassList.ShouldContain("mud-toggle-item-selected");
        selector.FindComponents<MudDateRangePicker>().ShouldBeEmpty();
    }

    [Fact]
    public void DateRangeSelector_WhenYearClicked_ShouldSelectTheYearUpToToday()
    {
        var selector = RenderSelector(new DateRangeSelection(DateRangePreset.Month, new DateOnly(2026, 10, 1), _today));

        Preset(selector, "Jahr").Click();

        _changes.ShouldBe([new DateRangeSelection(DateRangePreset.Year, new DateOnly(2026, 1, 1), _today)]);
    }

    [Fact]
    public async Task DateRangeSelector_WhenCustomClicked_ShouldOpenTheRangePickerAndReportTheChosenDays()
    {
        var selector = RenderSelector(new DateRangeSelection(DateRangePreset.Month, new DateOnly(2026, 10, 1), _today));

        Preset(selector, "Benutzerdefiniert").Click();

        var picker = selector.FindComponent<MudDateRangePicker>();
        picker.Instance.DateRange.ShouldBe(new DateRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 18)));
        picker.Instance.MaxDate.ShouldBe(new DateTime(2026, 10, 18));
        _changes.ShouldBeEmpty();

        await selector.InvokeAsync(() => picker.Instance.DateRangeChanged.InvokeAsync(new DateRange(new DateTime(2026, 9, 3), new DateTime(2026, 9, 7))));

        _changes.ShouldBe([DateRangeSelection.Custom(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 7))]);
    }

    [Fact]
    public async Task DateRangeSelector_WhenPickerHasOnlyAStart_ShouldWait()
    {
        var selector = RenderSelector(DateRangeSelection.Custom(new DateOnly(2026, 10, 1), _today));

        await selector.InvokeAsync(() => selector.FindComponent<MudDateRangePicker>().Instance.DateRangeChanged.InvokeAsync(new DateRange(new DateTime(2026, 9, 3), null)));

        _changes.ShouldBeEmpty();
    }

    private IRenderedComponent<DateRangeSelector> RenderSelector(DateRangeSelection value) =>
        Render<DateRangeSelector>(p => p
            .Add(s => s.Value, value)
            .Add(s => s.ValueChanged, (DateRangeSelection v) => _changes.Add(v)));

    private static AngleSharp.Dom.IElement Preset(IRenderedComponent<DateRangeSelector> selector, string label) =>
        selector.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == label);
}
