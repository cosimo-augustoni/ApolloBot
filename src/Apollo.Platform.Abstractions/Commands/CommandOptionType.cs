namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// The value type of a <see cref="CommandOptionDefinition"/>, and so of its entry in
/// <see cref="CommandInvocation.Options"/>.
/// </summary>
public enum CommandOptionType
{
    /// <summary>
    /// A <see cref="string"/>.
    /// </summary>
    String,

    /// <summary>
    /// A <see cref="long"/>.
    /// </summary>
    Integer,

    /// <summary>
    /// A <see cref="bool"/>.
    /// </summary>
    Boolean,
}
