namespace Apollo.Data.Tracking;

/// <summary>The effect of choosing a set of roles, before saving (#33). Members are current, non-bot members.</summary>
/// <param name="Members">The members holding at least one of the roles.</param>
/// <param name="NewlyTracked">If the roles were the tracking role: members who are not tracked now and would be.</param>
/// <param name="NoLongerTracked">If the roles were the tracking role: members who are tracked now and would no longer be.</param>
public sealed record RolePreview(int Members, int NewlyTracked, int NoLongerTracked);
