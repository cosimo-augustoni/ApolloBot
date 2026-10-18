namespace Apollo.Web.Client.Contracts;

/// <summary>Another member who was in the same voice channel at the same time, for <paramref name="Seconds"/> inside the range.</summary>
public sealed record VoiceBuddyDto(EventUserDto User, long Seconds);
