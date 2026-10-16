namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// A top-level command with its subcommand groups, subcommands and options, so an adapter can register it (Discord)
/// or parse and explain it (Fluxer) without knowing the handlers. Register with
/// <see cref="CommandServiceCollectionExtensions.AddCommandDefinition"/>.
/// </summary>
/// <remarks>
/// Several definitions may share a <see cref="Name"/>, e.g. one per feature for <c>apollo</c>;
/// <see cref="Combine"/> merges them into one command.
/// </remarks>
/// <param name="Name">Lower case, as the member types it, e.g. <c>apollo</c>.</param>
/// <param name="Description">Shown to the member while typing (German).</param>
public sealed record CommandDefinition(string Name, string Description)
{
    public IReadOnlyList<SubcommandGroupDefinition> Groups { get; init; } = [];

    public IReadOnlyList<SubcommandDefinition> Subcommands { get; init; } = [];

    /// <summary>
    /// Options of the command itself; only for a command without groups and subcommands.
    /// </summary>
    public IReadOnlyList<CommandOptionDefinition> Options { get; init; } = [];

    /// <summary>
    /// Every <see cref="CommandInvocation.Path"/> this command can produce.
    /// </summary>
    public IEnumerable<string> Paths =>
        Groups.Count == 0 && Subcommands.Count == 0
            ? [Name]
            : [
                .. Groups.SelectMany(group => group.Subcommands.Select(subcommand => $"{Name} {group.Name} {subcommand.Name}")),
                .. Subcommands.Select(subcommand => $"{Name} {subcommand.Name}"),
            ];

    /// <summary>
    /// Merges definitions with the same name into one command: their groups (by name), subcommands and options.
    /// The first definition of a name or group gives its description.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two definitions declare the same path.</exception>
    public static IReadOnlyList<CommandDefinition> Combine(IEnumerable<CommandDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var duplicate = definitions.SelectMany(definition => definition.Paths)
            .GroupBy(path => path, StringComparer.Ordinal)
            .FirstOrDefault(paths => paths.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"The command {duplicate.Key} is defined more than once.");
        }

        return [.. definitions.GroupBy(definition => definition.Name, StringComparer.Ordinal).Select(Merge)];
    }

    private static CommandDefinition Merge(IEnumerable<CommandDefinition> sameName)
    {
        var parts = sameName.ToList();
        return parts.Count == 1
            ? parts[0]
            : parts[0] with
            {
                Groups =
                [
                    .. parts.SelectMany(part => part.Groups)
                        .GroupBy(group => group.Name, StringComparer.Ordinal)
                        .Select(groups => groups.First() with { Subcommands = [.. groups.SelectMany(group => group.Subcommands)] }),
                ],
                Subcommands = [.. parts.SelectMany(part => part.Subcommands)],
                Options = [.. parts.SelectMany(part => part.Options)],
            };
    }
}
