namespace Apollo.AppHost;

/// <summary>
/// The single source of truth for the TimescaleDB container image.
/// Linked into Apollo.Testing (#7) so Testcontainers runs the same image as the AppHost.
/// </summary>
internal static class TimescaleImage
{
    public const string Image = "timescale/timescaledb";

    public const string Tag = "2.30.2-pg18";

    /// <summary>
    /// Where Postgres 18+ images expect the data mount; they keep data in a major-version subdirectory below it.
    /// </summary>
    public const string DataRoot = "/var/lib/postgresql";
}
