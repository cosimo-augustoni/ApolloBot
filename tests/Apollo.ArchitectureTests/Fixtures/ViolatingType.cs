using NetCord;

namespace Apollo.ArchitectureTests.Fixtures;

/// <summary>
/// Breaks the platform boundary on purpose: a NetCord type in a signature outside <c>Apollo.Platform.*</c>.
/// Only the negative tests evaluate this assembly.
/// </summary>
public sealed class ViolatingType
{
    public User? Author { get; init; }
}
