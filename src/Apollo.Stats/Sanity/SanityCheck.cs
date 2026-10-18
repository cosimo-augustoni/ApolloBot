namespace Apollo.Stats.Sanity;

/// <summary>The result of one sanity check: how many rows it found, and the newest of them as examples.</summary>
/// <param name="Count">How many rows the check found in the range.</param>
/// <param name="Examples">Up to <see cref="DataSanityQuery.MaxExamples"/> of them, newest first.</param>
public sealed record SanityCheck<T>(int Count, IReadOnlyList<T> Examples)
{
    public static SanityCheck<T> None { get; } = new(0, []);
}
