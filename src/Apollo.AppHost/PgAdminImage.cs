namespace Apollo.AppHost;

/// <summary>
/// The pgAdmin image added in publish mode. Pinned, so a redeploy never pulls a surprise upgrade.
/// </summary>
internal static class PgAdminImage
{
    public const string Image = "dpage/pgadmin4";

    public const string Tag = "9.18.0";
}
