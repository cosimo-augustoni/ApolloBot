namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// A member ran a command. Comes from a Discord slash command or, on Fluxer, a parsed text command
/// (<c>!apollo tracking on</c>), so it carries nothing that only one of them has.
/// </summary>
/// <param name="Guild">The guild the command was run in.</param>
/// <param name="Member">The member who ran it.</param>
/// <param name="Path">
/// The command, subcommand group and subcommand, separated by single spaces, e.g. <c>apollo tracking on</c>.
/// </param>
/// <param name="Options">
/// The option values by option name: <see cref="string"/>, <see cref="long"/> or <see cref="bool"/> per
/// <see cref="CommandOptionType"/>. Options the member left out are missing.
/// </param>
/// <param name="At">When the member ran the command.</param>
public sealed record CommandInvocation(
    PlatformRef Guild,
    PlatformRef Member,
    string Path,
    IReadOnlyDictionary<string, object?> Options,
    DateTimeOffset At);
