namespace Apollo.Web.Client.Contracts;

/// <summary>A <c>member_name_changes</c> row (scope §5.1, #114): the member's names after the change.</summary>
/// <param name="Kind">As stored: <c>Baseline</c> (tracking started) or <c>Changed</c>.</param>
/// <param name="Nickname">The server nickname; <see langword="null"/> without one.</param>
/// <param name="GlobalName">The global display name; <see langword="null"/> without one.</param>
/// <param name="Username">The account name.</param>
public sealed record MemberNameDetailsDto(string Kind, string? Nickname, string? GlobalName, string Username);
