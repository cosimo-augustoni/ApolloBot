namespace Apollo.Bot.Commands;

/// <summary>
/// The top-level <c>apollo</c> command that each feature adds its part to. Every part uses this description, because
/// <see cref="Platform.Abstractions.Commands.CommandDefinition.Combine"/> takes it from whichever part comes first.
/// </summary>
public static class ApolloCommand
{
    public const string Name = "apollo";

    /// <summary>
    /// Shown to the member while typing (German).
    /// </summary>
    public const string Description = "ApolloBot: deine Statistik und Datenerfassung";
}
