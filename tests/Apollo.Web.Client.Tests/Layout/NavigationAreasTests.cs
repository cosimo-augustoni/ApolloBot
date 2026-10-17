using Apollo.Web.Client.Layout;

namespace Apollo.Web.Client.Tests.Layout;

public sealed class NavigationAreasTests
{
    [Theory]
    [InlineData("admin", "Nav_BotStatus")]
    [InlineData("admin/dashboard", "Nav_ServerOverview")]
    [InlineData("/me/", "Nav_MemberOverview")]
    [InlineData("ME?tab=1#top", "Nav_MemberOverview")]
    [InlineData("datenschutz", "Nav_PrivacyPolicy")]
    public void Find_WhenPathBelongsToArea_ShouldReturnItsEntry(string path, string expectedLabelKey) =>
        NavigationAreas.Find(path).ShouldNotBeNull().LabelKey.ShouldBe(expectedLabelKey);

    [Theory]
    [InlineData("admin/unknown")]
    [InlineData("me/recaps")]
    [InlineData("me/privacy")]
    [InlineData("")]
    public void Find_WhenPathIsNoArea_ShouldReturnNull(string path) => NavigationAreas.Find(path).ShouldBeNull();

    [Fact]
    public void Sections_ShouldMatchTheWireframeDrawer() =>
        NavigationAreas.Sections.Select(s => $"{s.TitleKey}: {string.Join(", ", s.Items.Select(i => i.LabelKey))}").ShouldBe(
        [
            "Nav_SectionServer: Nav_BotStatus, Nav_ServerOverview, Nav_Members, Nav_ServerRecaps, Nav_Data, Nav_Settings",
            "Nav_SectionMember: Nav_MemberOverview",
            "Nav_SectionInfo: Nav_PrivacyPolicy",
        ]);

    [Fact]
    public void Items_ShouldMatchWireframeRouteMap() =>
        NavigationAreas.Items.Select(i => i.Href).ShouldBe(
        [
            "admin", "admin/dashboard", "admin/members", "admin/recaps", "admin/data", "admin/settings",
            "me",
            "datenschutz",
        ]);
}
