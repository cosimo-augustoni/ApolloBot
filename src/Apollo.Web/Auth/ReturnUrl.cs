namespace Apollo.Web.Auth;

/// <summary>Guards the <c>returnUrl</c> of the login against open redirects.</summary>
public static class ReturnUrl
{
    /// <summary>
    /// <paramref name="returnUrl"/> if it is a path on this site (<c>/...</c>, not <c>//host</c> or <c>/\host</c>, no
    /// control characters), otherwise <c>/</c>. Same rule as <c>IUrlHelper.IsLocalUrl</c>, without <c>~/</c>.
    /// </summary>
    public static string LocalOrRoot(string? returnUrl) => IsLocal(returnUrl) ? returnUrl! : "/";

    private static bool IsLocal(string? url) =>
        url is ['/', ..]
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.Any(char.IsControl);
}
