using System.Reflection;

namespace Apollo.ArchitectureTests;

/// <summary>
/// The platform boundary (scope §2, guidelines §5): NetCord and Fluxer.Net types appear only in <c>Apollo.Platform.*</c>.
/// </summary>
public static class PlatformBoundaryRule
{
    public const string AdapterPrefix = "Apollo.Platform.";

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] _platformLibraryPrefixes = ["NetCord", "Fluxer"];

    public static IReadOnlyList<string> FindViolations(Assembly assembly) => FindViolations(assembly, assembly.GetName().Name!);

    /// <summary>
    /// Evaluates <paramref name="assembly"/> as if it were named <paramref name="evaluatedAs"/>, so tests can prove that
    /// adapters are allowed without a real adapter referencing NetCord yet.
    /// </summary>
    /// <returns>
    /// One entry per referenced platform assembly (catches use inside method bodies too) and one per type whose
    /// signature uses a platform type (names the offending type).
    /// </returns>
    public static IReadOnlyList<string> FindViolations(Assembly assembly, string evaluatedAs)
    {
        if (evaluatedAs.StartsWith(AdapterPrefix, StringComparison.Ordinal))
        {
            return [];
        }

        var referencedAssemblies = assembly.GetReferencedAssemblies()
            .Where(reference => IsPlatformLibrary(reference.Name))
            .Select(reference => $"{evaluatedAs} references assembly {reference.Name}");

        var typeUses = assembly.GetTypes()
            .SelectMany(type => UsedTypes(type)
                .Where(used => IsPlatformLibrary(used.Namespace) || IsPlatformLibrary(used.Assembly.GetName().Name))
                .Select(used => used.FullName ?? used.Name)
                .Distinct()
                .Select(used => $"{type.FullName} uses {used}"));

        return [.. referencedAssemblies, .. typeUses];
    }

    private static bool IsPlatformLibrary(string? name) =>
        name is not null && _platformLibraryPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    private static IEnumerable<Type> UsedTypes(Type type)
    {
        IEnumerable<Type?> direct =
        [
            type.BaseType,
            .. type.GetInterfaces(),
            .. type.GetFields(Declared).Select(f => f.FieldType),
            .. type.GetProperties(Declared).Select(p => p.PropertyType),
            .. type.GetEvents(Declared).Select(e => e.EventHandlerType),
            .. type.GetMethods(Declared).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)),
            .. type.GetConstructors(Declared).SelectMany(c => c.GetParameters().Select(p => p.ParameterType)),
        ];

        return direct.OfType<Type>().SelectMany(Expand);
    }

    // Unwraps arrays, by-ref and pointer types and generic arguments: List<NetCord.User>[] uses NetCord.User.
    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
        {
            return Expand(type.GetElementType()!);
        }

        return type.IsConstructedGenericType
            ? type.GetGenericArguments().SelectMany(Expand).Prepend(type.GetGenericTypeDefinition())
            : [type];
    }
}
