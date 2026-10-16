namespace Apollo.Web.Auth;

/// <summary>The web host's cookie authentication schemes.</summary>
public static class AuthSchemes
{
    /// <summary>The session cookie with internal claims only (<see cref="ApolloPrincipal"/>).</summary>
    public const string Session = "Apollo.Session";

    /// <summary>
    /// The short-lived cookie a platform's OAuth handler signs in to after its callback; read and deleted by
    /// <c>/auth/complete</c>.
    /// </summary>
    public const string External = "Apollo.External";
}
