namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// A group of subcommands, e.g. <c>tracking</c> in <c>apollo tracking on</c>.
/// </summary>
/// <param name="Name">Lower case, as the member types it.</param>
/// <param name="Description">Shown to the member while typing (German).</param>
/// <param name="Subcommands">The subcommands of the group.</param>
public sealed record SubcommandGroupDefinition(string Name, string Description, IReadOnlyList<SubcommandDefinition> Subcommands);
