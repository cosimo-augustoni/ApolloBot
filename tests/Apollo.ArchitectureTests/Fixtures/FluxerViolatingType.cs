using Fluxer.StandIn;

namespace Apollo.ArchitectureTests.Fixtures;

/// <summary>
/// Breaks the platform boundary on purpose with a (stand-in) Fluxer type. Only the negative tests evaluate this assembly.
/// </summary>
public sealed class FluxerViolatingType
{
    public void Connect(FluxerClient client) => ArgumentNullException.ThrowIfNull(client);
}
