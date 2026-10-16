namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// Base of every neutral inbound event. Adapters map native gateway events to these records and deliver them through
/// <see cref="Dispatch.IPlatformEventDispatcher"/>.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened: the platform's timestamp if it has one, else the time it was received.</param>
public abstract record PlatformEvent(PlatformRef Guild, DateTimeOffset OccurredAt);
