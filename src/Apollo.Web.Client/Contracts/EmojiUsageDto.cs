namespace Apollo.Web.Client.Contracts;

/// <summary>How often an emoji was added as a reaction.</summary>
/// <param name="Name">The unicode emoji itself, or a custom emoji's latest name (empty when the platform no longer knows it).</param>
/// <param name="IsCustom">Whether it is a custom emoji of the server; its platform ID is not exposed.</param>
/// <param name="Count">How often it was added.</param>
public sealed record EmojiUsageDto(string Name, bool IsCustom, int Count);
