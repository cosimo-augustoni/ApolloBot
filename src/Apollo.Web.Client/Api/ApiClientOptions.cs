namespace Apollo.Web.Client.Api;

/// <summary>Client-side API settings, bound to <see cref="SectionName"/> in <c>wwwroot/appsettings*.json</c>.</summary>
public sealed class ApiClientOptions
{
    public const string SectionName = "Apollo:Web:Client";

    /// <summary>Use the mock clients in <c>Api/Mock</c> instead of calling the API.</summary>
    public bool UseMockData { get; set; }
}
