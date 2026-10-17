namespace Apollo.Data.Tracking;

/// <summary>The outcome of a <see cref="RoleSettingsStore"/> write.</summary>
public enum RoleSettingsWriteResult
{
    /// <summary>The settings now hold exactly the given roles.</summary>
    Saved,

    /// <summary>A role is unknown, deleted, or not a role of the guild on that platform; nothing changed.</summary>
    InvalidRole,

    /// <summary>
    /// Another admin changed the same settings at the same time (a row to delete was gone, or the one-tracking-role
    /// index rejected the insert); nothing of this write was kept.
    /// </summary>
    Conflict,
}
