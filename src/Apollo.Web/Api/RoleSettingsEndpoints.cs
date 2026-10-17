using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Admin;
using Apollo.Web.Auth;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Apollo.Web.Api;

/// <summary>
/// The role settings API of the admin page (#33, scope §5.1.2) under <c>/api/admin/settings</c>, in the
/// <see cref="AdminEndpoints.MapAdminGroup"/> group. A platform's settings belong to the guild the bot records there
/// (the configured guild of its identity provider, scope §4.3) and exist once the guild state sync has seen that guild;
/// any other platform is 404. Writes need the antiforgery header and record the signed-in admin.
/// </summary>
public static class RoleSettingsEndpoints
{
    public const string GroupPath = "/settings";

    /// <summary>The default and the largest page of <c>GET …/history</c>.</summary>
    public const int DefaultHistoryTake = 20;

    public const int MaxHistoryTake = 100;

    public static IEndpointRouteBuilder MapRoleSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var settings = endpoints.MapAdminGroup().MapGroup(GroupPath);

        settings.MapGet("/platforms", ListPlatformsAsync);
        settings.MapGet("/{platform}", GetAsync);
        settings.MapGet("/{platform}/preview", PreviewAsync);
        settings.MapGet("/{platform}/history", HistoryAsync);
        settings.MapPut("/{platform}/tracking-role", PutTrackingRoleAsync).AddEndpointFilter(ValidateAntiforgeryAsync);
        settings.MapPut("/{platform}/member-access-roles", PutMemberAccessRolesAsync).AddEndpointFilter(ValidateAntiforgeryAsync);

        return endpoints;
    }

    private static async Task<Ok<string[]>> ListPlatformsAsync(
        IEnumerable<IPlatformIdentityProvider> providers, RoleSettingsQuery query, CancellationToken cancellationToken)
    {
        var platforms = new List<string>();
        foreach (var provider in providers)
        {
            if (await query.FindSyncedGuildAsync(provider.Guild, cancellationToken) is not null)
            {
                platforms.Add(provider.Platform.ToString());
            }
        }

        return TypedResults.Ok(platforms.Distinct().ToArray());
    }

    private static async Task<Results<Ok<RoleSettingsDto>, NotFound>> GetAsync(
        string platform,
        IEnumerable<IPlatformIdentityProvider> providers,
        RoleSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (await FindGuildAsync(platform, providers, query, cancellationToken) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        var overview = await query.GetAsync(scope.GuildId, scope.Platform, cancellationToken);
        var trackingRole = overview.Roles.SingleOrDefault(r => r.Id == overview.TrackingRoleId);
        return TypedResults.Ok(new RoleSettingsDto(
            scope.Platform.ToString(),
            [.. overview.Roles.Select(r => new SettingsRoleDto(r.Id, r.Name ?? string.Empty, r.Color, r.Position, r.IsDeleted, r.MemberCount))],
            overview.TrackingRoleId,
            overview.MemberAccessRoleIds,
            overview.Mode.ToDto(),
            BotPermissionCheck.Evaluate(overview.Bot, trackingRole)));
    }

    private static async Task<Results<Ok<RolePreviewDto>, NotFound>> PreviewAsync(
        string platform,
        Guid[]? roleIds,
        IEnumerable<IPlatformIdentityProvider> providers,
        RoleSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (await FindGuildAsync(platform, providers, query, cancellationToken) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        var preview = await query.PreviewAsync(scope.GuildId, scope.Platform, roleIds ?? [], cancellationToken);
        return TypedResults.Ok(new RolePreviewDto(preview.Members, preview.NewlyTracked, preview.NoLongerTracked));
    }

    private static async Task<Results<Ok<RoleSettingChangeDto[]>, NotFound>> HistoryAsync(
        string platform,
        int? take,
        IEnumerable<IPlatformIdentityProvider> providers,
        RoleSettingsQuery query,
        CancellationToken cancellationToken)
    {
        if (await FindGuildAsync(platform, providers, query, cancellationToken) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        var history = await query.GetHistoryAsync(
            scope.GuildId, scope.Platform, Math.Clamp(take ?? DefaultHistoryTake, 1, MaxHistoryTake), cancellationToken);
        return TypedResults.Ok(history
            .Select(h => new RoleSettingChangeDto(
                h.ChangedAt,
                h.ChangedBy,
                h.Purpose == RoleSettingPurpose.Tracking ? RoleSettingKind.Tracking : RoleSettingKind.MemberAccess,
                h.Action == RoleSettingAction.Added ? RoleSettingChangeAction.Added : RoleSettingChangeAction.Removed,
                h.RoleName ?? string.Empty))
            .ToArray());
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem, Conflict>> PutTrackingRoleAsync(
        string platform,
        TrackingRoleRequest request,
        HttpContext http,
        IEnumerable<IPlatformIdentityProvider> providers,
        RoleSettingsQuery query,
        RoleSettingsStore store,
        CancellationToken cancellationToken)
    {
        if (await FindGuildAsync(platform, providers, query, cancellationToken) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        var result = await store.SetTrackingRoleAsync(scope.GuildId, scope.Platform, request.RoleId, AdminId(http), cancellationToken);
        return ToResult(result, nameof(TrackingRoleRequest.RoleId));
    }

    private static async Task<Results<NoContent, NotFound, ValidationProblem, Conflict>> PutMemberAccessRolesAsync(
        string platform,
        MemberAccessRolesRequest request,
        HttpContext http,
        IEnumerable<IPlatformIdentityProvider> providers,
        RoleSettingsQuery query,
        RoleSettingsStore store,
        CancellationToken cancellationToken)
    {
        if (await FindGuildAsync(platform, providers, query, cancellationToken) is not { } scope)
        {
            return TypedResults.NotFound();
        }

        var result = await store.SetMemberAccessRolesAsync(scope.GuildId, scope.Platform, request.RoleIds ?? [], AdminId(http), cancellationToken);
        return ToResult(result, nameof(MemberAccessRolesRequest.RoleIds));
    }

    /// <summary>
    /// Minimal API endpoints with a JSON body do not validate antiforgery tokens on their own; the BFF cookie (#29)
    /// would otherwise let another site send a write.
    /// </summary>
    private static async ValueTask<object?> ValidateAntiforgeryAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        return await antiforgery.IsRequestValidAsync(context.HttpContext)
            ? await next(context)
            : TypedResults.BadRequest();
    }

    private static async Task<(Guid GuildId, PlatformKind Platform)?> FindGuildAsync(
        string platform,
        IEnumerable<IPlatformIdentityProvider> providers,
        RoleSettingsQuery query,
        CancellationToken cancellationToken)
    {
        // Names only: Enum.TryParse would also take "0" or "1" for a platform.
        var name = Enum.GetNames<PlatformKind>().FirstOrDefault(n => string.Equals(n, platform, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return null;
        }

        var kind = Enum.Parse<PlatformKind>(name);
        if (providers.FirstOrDefault(p => p.Platform == kind) is not { } provider
            || await query.FindSyncedGuildAsync(provider.Guild, cancellationToken) is not { } guildId)
        {
            return null;
        }

        return (guildId, kind);
    }

    /// <summary>Saved: 204. An invalid role: 400 with a validation problem. Another admin saved at the same time: 409.</summary>
    private static Results<NoContent, NotFound, ValidationProblem, Conflict> ToResult(RoleSettingsWriteResult result, string field) => result switch
    {
        RoleSettingsWriteResult.Saved => TypedResults.NoContent(),
        RoleSettingsWriteResult.Conflict => TypedResults.Conflict(),
        _ => TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = ["The role is unknown, deleted, or not a role of this guild on this platform."],
        }),
    };

    private static Guid AdminId(HttpContext http) =>
        ApolloPrincipal.ReadSession(http.User)?.UserId
            ?? throw new InvalidOperationException("The admin policy let a request without a session principal through.");
}
