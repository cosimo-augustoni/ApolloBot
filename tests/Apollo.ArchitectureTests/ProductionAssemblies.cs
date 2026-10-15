using System.Reflection;

namespace Apollo.ArchitectureTests;

/// <summary>
/// The production <c>Apollo.*</c> assemblies this project references (every <c>src/</c> project except the AppHost).
/// </summary>
public static class ProductionAssemblies
{
    private static readonly string[] _testAssemblies = ["Apollo.ArchitectureTests", "Apollo.Testing"];

    public static IReadOnlyList<Assembly> All { get; } =
    [
        .. Directory.GetFiles(AppContext.BaseDirectory, "Apollo.*.dll")
            .Where(path => !_testAssemblies.Contains(Path.GetFileNameWithoutExtension(path)))
            .Order(StringComparer.Ordinal)
            .Select(path => Assembly.LoadFrom(path)),
    ];

    public static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApolloBot.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
}
