namespace Apollo.Web.Client.Contracts;

/// <summary>One check of the data sanity report (#40).</summary>
/// <param name="Count">How many rows the check found.</param>
/// <param name="Examples">Up to 20 of them, newest first.</param>
public sealed record SanityCheckDto<T>(int Count, IReadOnlyList<T> Examples);
