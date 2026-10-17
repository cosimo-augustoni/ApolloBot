using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>
/// <see cref="MockRoleSettingsClient"/> whose previews the test controls: each call waits for <see cref="Preview"/>,
/// which may fail or finish out of order, as a slow or failing host would.
/// </summary>
internal sealed class ControlledPreviewClient(MockRoleSettingsClient inner) : IRoleSettingsClient
{
    /// <summary>Answers a preview for the given role IDs; by default the mock's answer.</summary>
    public Func<IReadOnlyCollection<Guid>, Task<RolePreviewDto>>? Preview { get; set; }

    public Task<IReadOnlyList<string>> GetPlatformsAsync(CancellationToken cancellationToken) => inner.GetPlatformsAsync(cancellationToken);

    public Task<RoleSettingsDto> GetSettingsAsync(string platform, CancellationToken cancellationToken) => inner.GetSettingsAsync(platform, cancellationToken);

    public Task<RolePreviewDto> GetPreviewAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        Preview is { } preview ? preview(roleIds) : inner.GetPreviewAsync(platform, roleIds, cancellationToken);

    public Task<IReadOnlyList<RoleSettingChangeDto>> GetHistoryAsync(string platform, int take, CancellationToken cancellationToken) =>
        inner.GetHistoryAsync(platform, take, cancellationToken);

    public Task SetTrackingRoleAsync(string platform, Guid? roleId, CancellationToken cancellationToken) =>
        inner.SetTrackingRoleAsync(platform, roleId, cancellationToken);

    public Task SetMemberAccessRolesAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
        inner.SetMemberAccessRolesAsync(platform, roleIds, cancellationToken);
}
