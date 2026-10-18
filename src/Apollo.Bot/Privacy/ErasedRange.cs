namespace Apollo.Bot.Privacy;

/// <summary>
/// The earliest and latest <c>occurred_at</c> of the rows a data deletion deletes or clears a reference in (for a session,
/// its start), so the continuous aggregates over them can be refreshed after the commit (#39).
/// </summary>
public sealed record ErasedRange(DateTimeOffset From, DateTimeOffset To)
{
    public ErasedRange Union(ErasedRange other) =>
        new(From < other.From ? From : other.From, To > other.To ? To : other.To);
}
