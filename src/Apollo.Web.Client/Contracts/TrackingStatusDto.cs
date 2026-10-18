using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>Whether and since when a member is tracked (scope §5.1.1), as Zurich days.</summary>
/// <param name="TrackedSince">The day their current tracking started; <see langword="null"/> when they are not tracked now.</param>
/// <param name="TrackedUntil">The day their latest earlier tracking ended; <see langword="null"/> when it never ended.</param>
public sealed record TrackingStatusDto(DateOnly? TrackedSince, DateOnly? TrackedUntil)
{
    /// <summary>Whether they are tracked now.</summary>
    [JsonIgnore]
    public bool IsTracked => TrackedSince is not null;

    /// <summary>Whether they were ever tracked, so there can be data of theirs.</summary>
    [JsonIgnore]
    public bool WasEverTracked => TrackedSince is not null || TrackedUntil is not null;
}
