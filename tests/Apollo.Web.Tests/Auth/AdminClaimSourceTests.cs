using System.Text.RegularExpressions;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Tests.Auth;

/// <summary>
/// Admin access is hard-wired to the Administrator permission (D8, scope §5.1.2): the admin claim has exactly one
/// source, <c>AccessRevalidator</c>, which reads it from the synced guild state. A second place that writes the claim
/// (a role setting, an allowlist, a config value) fails here.
/// </summary>
public sealed partial class AdminClaimSourceTests
{
    private static readonly DirectoryInfo _src = FindSourceDirectory();

    [Fact]
    public void AdminClaimType_ShouldBeSpelledOutOnlyInApolloClaimTypes()
    {
        var files = SourceFilesContaining(ApolloClaimTypes.Admin);

        files.ShouldBe(["Apollo.Web.Client/Contracts/ApolloClaimTypes.cs"]);
    }

    [Fact]
    public void AdminClaim_ShouldBeAddedOnlyByApolloPrincipalWithAccess()
    {
        var files = SourceFilesMatching(AdminClaimUse());

        files.ShouldBe(
        [
            "Apollo.Web.Client/Auth/ApolloAuthenticationStateProvider.cs",
            "Apollo.Web.Client/Auth/ApolloPolicies.cs",
            "Apollo.Web/Auth/ApolloPrincipal.cs",
        ], ignoreOrder: true);
    }

    [Fact]
    public void AccessCheckResult_ShouldBeWrittenOnlyByAccessRevalidator()
    {
        var files = SourceFilesMatching(WithAccessCall());

        files.ShouldBe(["Apollo.Web/Auth/AccessRevalidator.cs"]);
    }

    private static List<string> SourceFilesContaining(string text) => SourceFilesMatching(new Regex(Regex.Escape($"\"{text}\"")));

    private static List<string> SourceFilesMatching(Regex pattern) =>
        _src.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => f.Extension is ".cs" or ".razor")
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => pattern.IsMatch(File.ReadAllText(f.FullName)))
            .Select(f => Path.GetRelativePath(_src.FullName, f.FullName).Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();

    private static DirectoryInfo FindSourceDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = new DirectoryInfo(Path.Combine(dir.FullName, "src"));
            if (candidate.Exists && File.Exists(Path.Combine(dir.FullName, "ApolloBot.slnx")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The src directory was not found above the test output.");
    }

    // Not in XML doc references (<see cref="ApolloClaimTypes.Admin"/>), which only describe the claim.
    [GeneratedRegex(@"(?<!cref="")ApolloClaimTypes\.Admin\b")]
    private static partial Regex AdminClaimUse();

    [GeneratedRegex(@"ApolloPrincipal\.WithAccess\(")]
    private static partial Regex WithAccessCall();
}
