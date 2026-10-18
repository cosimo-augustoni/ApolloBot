namespace Apollo.Web.Client.Components.Stats;

/// <summary>One row of a <see cref="RankList"/>.</summary>
/// <param name="Label">What is ranked, e.g. a member or an emoji.</param>
/// <param name="Value">The formatted number.</param>
/// <param name="Share">The row's value against the first row's, 0 to 1, for the bar behind it.</param>
public sealed record RankItem(string Label, string Value, double Share)
{
    /// <summary>
    /// The rows of <paramref name="items"/>, in their order (most first), each bar relative to the largest value.
    /// </summary>
    public static IReadOnlyList<RankItem> Of<T>(IEnumerable<T> items, Func<T, string> label, Func<T, long> value, Func<long, string> format)
    {
        var list = items.ToList();
        var max = list.Select(value).DefaultIfEmpty().Max();
        return [.. list.Select(item => new RankItem(label(item), format(value(item)), max <= 0 ? 0 : value(item) / (double)max))];
    }
}
