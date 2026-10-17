using System.Text.RegularExpressions;
using System.Xml.Linq;
using Apollo.Web.Client.Layout;
using Apollo.Web.Client.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Apollo.Web.Client.Tests.Resources;

public sealed partial class StringsTests : ApolloTestContext
{
    private static readonly DirectoryInfo _clientProject = FindClientProject();

    /// <summary>Files whose visible text must come from <c>Strings.resx</c> (issue #28).</summary>
    private static readonly string[] _localizedFiles =
        ["Layout/MainLayout.razor", "Layout/NavMenu.razor", "Layout/UserMenu.razor", "Pages/Home.razor", "Pages/NotFound.razor", "Pages/NoAccess.razor",
         "Pages/Placeholder.razor", "Pages/Admin/AdminPlaceholder.razor", "Components/AreaPlaceholder.razor", "Pages/Admin/Settings.razor",
         "Pages/Admin/RoleSettings/RoleSettingsPanel.razor", "Pages/Admin/RoleSettings/TrackingRoleCard.razor", "Pages/Admin/RoleSettings/MemberAccessCard.razor",
         "Pages/Admin/RoleSettings/RoleSettingsHistory.razor", "Pages/Admin/RoleSettings/RoleLabel.razor"];

    [Fact]
    public void Strings_ShouldContainAllKeysUsedInRazorFiles()
    {
        var keys = ResourceKeys();

        var used = _clientProject.EnumerateFiles("*.razor", SearchOption.AllDirectories)
            .SelectMany(f => LocalizerKey().Matches(File.ReadAllText(f.FullName)).Select(m => $"{f.Name}: {m.Groups["key"].Value}"))
            .ToList();

        used.ShouldNotBeEmpty();
        used.Where(u => !keys.Contains(u[(u.IndexOf(": ", StringComparison.Ordinal) + 2)..])).ShouldBeEmpty();
    }

    [Fact]
    public void Strings_ShouldContainAllNavigationKeys()
    {
        var keys = ResourceKeys();

        var navigationKeys = NavigationAreas.Sections
            .SelectMany(s => s.Items.SelectMany(i => new[] { i.LabelKey, i.TitleKey }).Append(s.TitleKey))
            .OfType<string>();

        navigationKeys.Where(k => !keys.Contains(k)).ShouldBeEmpty();
    }

    [Fact]
    public void Localizer_ShouldFindEveryResourceKey()
    {
        var localizer = Services.GetRequiredService<IStringLocalizer<Strings>>();

        foreach (var key in ResourceKeys())
        {
            var text = localizer[key];
            text.ResourceNotFound.ShouldBeFalse(key);
            text.Value.ShouldNotBeNullOrWhiteSpace(key);
        }
    }

    [Fact]
    public void LayoutAndPages_ShouldNotContainTextLiterals()
    {
        foreach (var file in _localizedFiles)
        {
            var markup = File.ReadAllText(Path.Combine(_clientProject.FullName, file));
            var withoutCode = RazorCode().Replace(markup, string.Empty);

            TextBetweenTags().Matches(withoutCode).Select(m => m.Value.Trim()).Where(t => t.Length > 0).ShouldBeEmpty(file);
            TextAttribute().Matches(withoutCode).Select(m => m.Value).ShouldBeEmpty(file);
        }
    }

    private static HashSet<string> ResourceKeys() =>
        XDocument.Load(Path.Combine(_clientProject.FullName, "Resources", "Strings.resx"))
            .Root!.Elements("data").Select(d => (string)d.Attribute("name")!).ToHashSet();

    private static DirectoryInfo FindClientProject()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = new DirectoryInfo(Path.Combine(dir.FullName, "src", "Apollo.Web.Client"));
            if (candidate.Exists)
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("src/Apollo.Web.Client not found above the test output folder.");
    }

    [GeneratedRegex("""L\["(?<key>[^"]+)"\]""")]
    private static partial Regex LocalizerKey();

    /// <summary>Razor comments, the <c>@code</c> block, directives, control flow (also <c>else</c> and <c>case</c> lines inside it) and inline expressions like <c>@L["Key"]</c>.</summary>
    [GeneratedRegex("""@\*[\s\S]*?\*@|@code\s*\{[\s\S]*|^\s*@(page|layout|inherits|inject|implements|using|attribute|rendermode)\b[^\n]*|@(foreach|if|for|while|switch|else)\b[^\n]*|^\s*(else|case|break|default)\b[^\n]*|^\s*[{}]\s*$|@\([^)]*\)|@[\w.]+(\[([^\[\]]|\[[^\]]*\])*\])*(\([^)]*\))?""", RegexOptions.Multiline)]
    private static partial Regex RazorCode();

    [GeneratedRegex("""(?<=>)[^<>]+(?=<)""")]
    private static partial Regex TextBetweenTags();

    /// <summary>Attributes that browsers show or read aloud, with a literal value.</summary>
    [GeneratedRegex("""\b(aria-label|title|alt|placeholder|Title|Text|Label|Explanation)="[^"@]+\x22""")]
    private static partial Regex TextAttribute();
}
