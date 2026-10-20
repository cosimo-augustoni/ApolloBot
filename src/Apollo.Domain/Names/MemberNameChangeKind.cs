namespace Apollo.Domain.Names;

/// <summary>
/// Why a <c>member_name_changes</c> row was written (<c>member_name_changes.kind</c>).
/// </summary>
public enum MemberNameChangeKind
{
    /// <summary>The names when tracking started: the first row of every tracking period.</summary>
    Baseline,

    /// <summary>One of the names differs from the member's previous row.</summary>
    Changed,
}
