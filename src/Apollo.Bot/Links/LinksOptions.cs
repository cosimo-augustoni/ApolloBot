using System.ComponentModel.DataAnnotations;

namespace Apollo.Bot.Links;

/// <summary>
/// Settings under <c>Apollo:Links</c>: where the website is, so the bot can link its pages. The AppHost sets it.
/// </summary>
public sealed class LinksOptions : IValidatableObject
{
    public const string SectionName = "Apollo:Links";

    /// <summary>
    /// The public address of <c>apollo-web</c> (the reverse proxy in front of it), e.g. <c>https://apollo.example.ch</c>.
    /// Every website link is built from it, so no full page URL is configured by hand.
    /// </summary>
    [Required(ErrorMessage = $"{SectionName}:{nameof(PublicWebUrl)} is required.")]
    public string? PublicWebUrl { get; set; }

    /// <summary>
    /// The German privacy page (<c>/datenschutz</c>), linked from every command reply (scope §5.4).
    /// </summary>
    /// <remarks>A method, not a property: options validation reads every property, valid or not.</remarks>
    public Uri GetPrivacyPageUrl() => Page("datenschutz");

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PublicWebUrl is not null && !TryGetBaseUrl(out _))
        {
            yield return new ValidationResult(
                $"{SectionName}:{nameof(PublicWebUrl)} must be an absolute http or https URL.",
                [nameof(PublicWebUrl)]);
        }
    }

    private Uri Page(string path) =>
        TryGetBaseUrl(out var baseUrl)
            ? new Uri($"{baseUrl.AbsoluteUri.TrimEnd('/')}/{path}")
            : throw new InvalidOperationException($"{SectionName}:{nameof(PublicWebUrl)} is not a valid URL.");

    private bool TryGetBaseUrl(out Uri baseUrl) =>
        Uri.TryCreate(PublicWebUrl, UriKind.Absolute, out baseUrl!)
        && (baseUrl.Scheme == Uri.UriSchemeHttp || baseUrl.Scheme == Uri.UriSchemeHttps);
}
