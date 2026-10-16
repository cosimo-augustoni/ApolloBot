namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// An option of a command or subcommand.
/// </summary>
/// <param name="Name">Lower case, as the member types it.</param>
/// <param name="Description">Shown to the member while typing (German).</param>
/// <param name="Type">The value type.</param>
/// <param name="Required">Whether the member must give it.</param>
public sealed record CommandOptionDefinition(string Name, string Description, CommandOptionType Type, bool Required = false);
