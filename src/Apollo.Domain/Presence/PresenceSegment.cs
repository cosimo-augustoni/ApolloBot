namespace Apollo.Domain.Presence;

/// <summary>
/// A stretch of a presence session with unchanged rich presence (#115). The segments of a closed session cover it from
/// start to end without gaps.
/// </summary>
public sealed record PresenceSegment(DateTimeOffset From, DateTimeOffset To, PresenceActivityDetails Details);
