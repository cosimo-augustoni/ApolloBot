using Microsoft.AspNetCore.Components;

namespace Apollo.Web.Client.Auth;

public static class LoginNavigation
{
    /// <summary>
    /// Reloads into the host's <c>/auth/login</c> with the current page as <c>returnUrl</c>. A full page load: the host
    /// sets the HttpOnly session cookie (BFF, #29).
    /// </summary>
    public static void NavigateToLogin(this NavigationManager navigation)
    {
        ArgumentNullException.ThrowIfNull(navigation);

        var returnUrl = "/" + navigation.ToBaseRelativePath(navigation.Uri);
        navigation.NavigateTo($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
    }
}
