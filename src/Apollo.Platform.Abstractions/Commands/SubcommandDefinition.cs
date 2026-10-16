namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// A subcommand, e.g. <c>on</c> in <c>apollo tracking on</c>.
/// </summary>
/// <param name="Name">Lower case, as the member types it.</param>
/// <param name="Description">Shown to the member while typing (German).</param>
public sealed record SubcommandDefinition(string Name, string Description)
{
    public IReadOnlyList<CommandOptionDefinition> Options { get; init; } = [];
}
