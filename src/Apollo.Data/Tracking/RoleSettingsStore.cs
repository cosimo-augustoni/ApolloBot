using System.Globalization;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Apollo.Data.Tracking;

/// <summary>
/// Writes the role settings of one <c>(guild, platform)</c> for the admin page (#33, scope §5.1.2), in exactly the shape
/// the bot's 30 s reload reads (<c>role_settings</c>). Every write runs in one transaction that first tells the
/// <c>role_settings_audit</c> trigger who changes the settings and when, so its <c>Removed</c> entries name the admin and
/// the <see cref="TimeProvider"/> time like its <c>Added</c> entries.
/// </summary>
public sealed class RoleSettingsStore(ApolloDbContext db, TimeProvider time)
{
    /// <summary>
    /// The transaction-local settings the audit trigger reads for a removal (migration <c>AddRoleSettingsPage</c>).
    /// </summary>
    public const string ChangedBySetting = "apollo.role_settings_changed_by";

    /// <inheritdoc cref="ChangedBySetting"/>
    public const string ChangedAtSetting = "apollo.role_settings_changed_at";

    /// <summary>
    /// Makes <paramref name="roleId"/> the tracking role, replacing the current one; <see langword="null"/> clears it
    /// (mode <see cref="TrackingMode.NoTrackingRole"/>: nobody is tracked). Saving the current role changes nothing.
    /// </summary>
    public Task<RoleSettingsWriteResult> SetTrackingRoleAsync(
        Guid guildId,
        PlatformKind platform,
        Guid? roleId,
        Guid changedByUserId,
        CancellationToken cancellationToken) =>
        ReplaceAsync(guildId, platform, RoleSettingPurpose.Tracking, roleId is { } id ? [id] : [], changedByUserId, cancellationToken);

    /// <summary>Makes <paramref name="roleIds"/> (duplicates ignored) exactly the member access roles.</summary>
    public Task<RoleSettingsWriteResult> SetMemberAccessRolesAsync(
        Guid guildId,
        PlatformKind platform,
        IReadOnlyCollection<Guid> roleIds,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        return ReplaceAsync(guildId, platform, RoleSettingPurpose.MemberAccess, roleIds, changedByUserId, cancellationToken);
    }

    private async Task<RoleSettingsWriteResult> ReplaceAsync(
        Guid guildId,
        PlatformKind platform,
        RoleSettingPurpose purpose,
        IReadOnlyCollection<Guid> roleIds,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var wanted = roleIds.ToHashSet();
        var valid = await db.Roles
            .Where(r => wanted.Contains(r.Id) && r.GuildId == guildId && r.DeletedAt == null)
            .Where(r => db.RolePlatformIds.Any(p => p.RoleId == r.Id && p.Platform == platform))
            .CountAsync(cancellationToken);
        if (valid != wanted.Count)
        {
            return RoleSettingsWriteResult.InvalidRole;
        }

        try
        {
            await WriteAsync(guildId, platform, purpose, wanted, changedByUserId, cancellationToken);
            return RoleSettingsWriteResult.Saved;
        }
        catch (DbUpdateException exception) when (exception is DbUpdateConcurrencyException || IsUniqueViolation(exception))
        {
            // Another admin saved the same settings at the same time: a row this write deletes was already gone, or the
            // one-tracking-role index rejected the new row. The transaction rolled back; the caller reloads.
            db.ChangeTracker.Clear();
            return RoleSettingsWriteResult.Conflict;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private async Task WriteAsync(
        Guid guildId,
        PlatformKind platform,
        RoleSettingPurpose purpose,
        HashSet<Guid> wanted,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // set_config(..., true) lasts until the end of this transaction only, so a pooled connection never carries the
        // author into a later, unrelated delete.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT set_config({ChangedBySetting}, {changedByUserId.ToString()}, true),
                   set_config({ChangedAtSetting}, {now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture)}, true)
            """,
            cancellationToken);

        var current = await db.RoleSettings
            .Where(s => s.GuildId == guildId && s.Platform == platform && s.Purpose == purpose)
            .ToListAsync(cancellationToken);

        // Removals first: the one-tracking-role index would reject a new tracking row next to the old one.
        db.RoleSettings.RemoveRange(current.Where(s => !wanted.Contains(s.RoleId)));
        await db.SaveChangesAsync(cancellationToken);

        db.RoleSettings.AddRange(wanted
            .Except(current.Select(s => s.RoleId))
            .Select(roleId => new RoleSetting
            {
                GuildId = guildId,
                Platform = platform,
                RoleId = roleId,
                Purpose = purpose,
                ChangedByUserId = changedByUserId,
                ChangedAt = now,
            }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
