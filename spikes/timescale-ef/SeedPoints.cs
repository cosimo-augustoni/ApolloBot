namespace Apollo.Spikes.TimescaleEf;

/// <summary>
/// One event per channel, so each boundary case can be asserted on its own aggregate row.
/// </summary>
public static class SeedPoints
{
    public static IReadOnlyList<SeedPoint> All { get; } =
    [
        new(1, "2026-01-14T22:59:00Z"),  // Zurich 14 Jan 23:59 (CET)
        new(2, "2026-01-14T23:01:00Z"),  // Zurich 15 Jan 00:01 (CET)
        new(3, "2026-03-28T23:30:00Z"),  // Zurich 29 Mar 00:30 (CET), spring-forward day
        new(4, "2026-03-29T21:59:00Z"),  // Zurich 29 Mar 23:59 (CEST), last minute of the 23 h day
        new(5, "2026-03-29T22:30:00Z"),  // Zurich 30 Mar 00:30 (CEST)
        new(6, "2026-10-24T22:30:00Z"),  // Zurich 25 Oct 00:30 (CEST), fall-back day
        new(7, "2026-10-25T00:30:00Z"),  // Zurich 25 Oct 02:30 (CEST), first pass of the repeated hour
        new(8, "2026-10-25T01:30:00Z"),  // Zurich 25 Oct 02:30 (CET), second pass of the repeated hour
        new(9, "2026-10-25T22:30:00Z"),  // Zurich 25 Oct 23:30 (CET), last hour of the 25 h day
        new(10, "2026-10-25T23:00:00Z"), // Zurich 26 Oct 00:00 (CET)
    ];
}
