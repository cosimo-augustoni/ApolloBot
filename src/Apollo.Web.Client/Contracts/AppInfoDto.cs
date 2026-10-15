namespace Apollo.Web.Client.Contracts;

/// <summary>Response of <c>GET /api/app-info</c>: which build is running and where.</summary>
/// <param name="Version">Informational version of the web host, without the source revision.</param>
/// <param name="Environment">Hosting environment name, e.g. <c>Production</c> or <c>Development</c>.</param>
public sealed record AppInfoDto(string Version, string Environment);
