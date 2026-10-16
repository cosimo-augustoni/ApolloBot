using System.Diagnostics.CodeAnalysis;

namespace Apollo.Platform.Abstractions.Commands;

/// <summary>
/// The id of a <see cref="ReplyButton"/>: <c>prefix:owner</c> or <c>prefix:owner:argument</c>. The prefix picks the
/// <see cref="IButtonHandler"/>; the owner is the platform id of the member the button was issued to, so the router
/// can turn everyone else away.
/// </summary>
/// <param name="Prefix">The <see cref="IButtonHandler.IdPrefix"/>. Not blank, no <c>:</c>.</param>
/// <param name="OwnerId">The <see cref="PlatformRef.ExternalId"/> of the member the button is for. Not blank, no <c>:</c>.</param>
/// <param name="Argument">Optional data for the handler, e.g. a one-time code. May contain <c>:</c>.</param>
public sealed record ButtonId(string Prefix, string OwnerId, string? Argument = null)
{
    /// <summary>
    /// The longest id every platform accepts (Discord's <c>custom_id</c> limit).
    /// </summary>
    public const int MaxLength = 100;

    private const char Separator = ':';

    public string Prefix { get; init => field = Segment(value, nameof(Prefix)); } = Segment(Prefix, nameof(Prefix));

    public string OwnerId { get; init => field = Segment(value, nameof(OwnerId)); } = Segment(OwnerId, nameof(OwnerId));

    public static ButtonId For(string prefix, PlatformRef owner, string? argument = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new ButtonId(prefix, owner.ExternalId, argument);
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out ButtonId? buttonId)
    {
        buttonId = null;
        var parts = value?.Split(Separator, 3);
        if (parts is not { Length: >= 2 } || parts.Take(2).Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        buttonId = new ButtonId(parts[0], parts[1], parts.Length == 3 ? parts[2] : null);
        return true;
    }

    /// <summary>
    /// Whether the button was issued to <paramref name="member"/>.
    /// </summary>
    public bool IsFor(PlatformRef member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.ExternalId == OwnerId;
    }

    /// <exception cref="InvalidOperationException">The id is longer than <see cref="MaxLength"/>.</exception>
    public override string ToString()
    {
        var id = Argument is null ? $"{Prefix}{Separator}{OwnerId}" : $"{Prefix}{Separator}{OwnerId}{Separator}{Argument}";
        return id.Length <= MaxLength
            ? id
            : throw new InvalidOperationException($"The button id {id} is longer than {MaxLength} characters.");
    }

    private static string Segment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains(Separator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"A button id {parameterName} must not contain '{Separator}'.", parameterName);
        }

        return value;
    }
}
