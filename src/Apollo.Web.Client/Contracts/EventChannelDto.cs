namespace Apollo.Web.Client.Contracts;

/// <summary>A channel or thread as the admin data page shows it (#32).</summary>
/// <param name="Id">The internal channel ID; platform IDs are never exposed.</param>
/// <param name="Name">The synced name; <see langword="null"/> before the guild sync named the channel.</param>
public sealed record EventChannelDto(Guid Id, string? Name);
