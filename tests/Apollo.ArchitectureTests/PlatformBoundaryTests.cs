using Apollo.ArchitectureTests.Fixtures;

namespace Apollo.ArchitectureTests;

public sealed class PlatformBoundaryTests
{
    private static readonly System.Reflection.Assembly _fixtures = typeof(ViolatingType).Assembly;

    public static TheoryData<string> ProductionAssemblyNames => [.. ProductionAssemblies.All.Select(a => a.GetName().Name!)];

    [Fact]
    public void ProductionAssemblies_ShouldIncludeEverySrcProjectExceptAppHost()
    {
        var srcProjects = Directory.GetDirectories(Path.Combine(ProductionAssemblies.RepositoryRoot, "src"))
            .Select(Path.GetFileName)
            .Where(name => name != "Apollo.AppHost");

        ProductionAssemblies.All.Select(a => a.GetName().Name).ShouldBe(srcProjects, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(ProductionAssemblyNames))]
    public void ProductionAssemblies_OutsidePlatformAdapters_ShouldNotReferencePlatformLibraries(string assemblyName)
    {
        var assembly = ProductionAssemblies.All.Single(a => a.GetName().Name == assemblyName);

        PlatformBoundaryRule.FindViolations(assembly).ShouldBeEmpty();
    }

    [Fact]
    public void Rule_WhenAssemblyUsesNetCordType_ShouldReportViolation()
    {
        var violations = PlatformBoundaryRule.FindViolations(_fixtures);

        violations.ShouldContain(v => v.Contains(typeof(ViolatingType).FullName!) && v.Contains("NetCord.User"));
        violations.ShouldContain(v => v.Contains("references assembly NetCord"));
    }

    [Fact]
    public void Rule_WhenAssemblyUsesFluxerType_ShouldReportViolation()
    {
        var violations = PlatformBoundaryRule.FindViolations(_fixtures);

        violations.ShouldContain(v => v.Contains(typeof(FluxerViolatingType).FullName!) && v.Contains("Fluxer.StandIn.FluxerClient"));
    }

    [Fact]
    public void Rule_WhenPlatformAdapterUsesNetCord_ShouldNotReportViolation()
    {
        var violations = PlatformBoundaryRule.FindViolations(_fixtures, evaluatedAs: "Apollo.Platform.Discord");

        violations.ShouldBeEmpty();
    }
}
