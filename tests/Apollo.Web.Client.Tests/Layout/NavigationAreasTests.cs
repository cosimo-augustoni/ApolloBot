using Apollo.Web.Client.Layout;

namespace Apollo.Web.Client.Tests.Layout;

public sealed class NavigationAreasTests
{
    [Theory]
    [InlineData("admin/bot-status", "Nav_BotStatus")]
    [InlineData("/me/privacy/", "Nav_Privacy")]
    [InlineData("ME?tab=1#top", "Nav_Overview")]
    [InlineData("", "Nav_Home")]
    public void Find_WhenPathBelongsToArea_ShouldReturnItsEntry(string path, string expectedLabelKey) =>
        NavigationAreas.Find(path).ShouldNotBeNull().LabelKey.ShouldBe(expectedLabelKey);

    [Fact]
    public void Find_WhenPathIsUnknown_ShouldReturnNull() => NavigationAreas.Find("admin/unknown").ShouldBeNull();

    [Fact]
    public void Items_ShouldHaveUniqueRoutes() =>
        NavigationAreas.Items.Select(i => i.Href).ShouldBeUnique();
}
