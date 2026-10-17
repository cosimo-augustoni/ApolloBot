using System.Net;
using System.Text.Json;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpRoleSettingsClientTests
{
    private static readonly Guid _statistik = Guid.Parse("0b6f3d4e-1a2b-4c3d-8e9f-a0b1c2d3e4f5");
    private static readonly Guid _mitglied = Guid.Parse("1c7a4e5f-2b3c-4d4e-9fa0-b1c2d3e4f5a6");
    private static readonly AntiforgeryTokenDto _antiforgery = new("X-XSRF-TOKEN", "request-token");

    // The host's TypedResults.ValidationProblem for a rejected role.
    private static readonly object _validationProblem = new
    {
        type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        title = "One or more validation errors occurred.",
        status = 400,
        errors = new Dictionary<string, string[]> { ["RoleId"] = ["The role is unknown, deleted, or not a role of this guild on this platform."] },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetPlatforms_ShouldReadThePlatformList()
    {
        var handler = new StubHttpMessageHandler(new[] { "Discord" });

        var platforms = await new HttpRoleSettingsClient(handler.CreateClient()).GetPlatformsAsync(Ct);

        platforms.ShouldBe(["Discord"]);
        handler.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/admin/settings/platforms");
    }

    [Fact]
    public async Task GetSettings_ShouldReadTheSettingsOfThePlatform()
    {
        var role = new SettingsRoleDto(_statistik, "Statistik", 0x3498DB, 2, IsDeleted: false, MemberCount: 168);
        var settings = new RoleSettingsDto("Discord", [role], _statistik, [], RoleSettingsMode.Active, new BotPermissionCheckDto(true, true));
        var handler = new StubHttpMessageHandler(settings);

        var result = await new HttpRoleSettingsClient(handler.CreateClient()).GetSettingsAsync("Discord", Ct);

        result.Roles.ShouldBe(settings.Roles);
        (result with { Roles = settings.Roles, MemberAccessRoleIds = settings.MemberAccessRoleIds }).ShouldBe(settings);
        handler.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/api/admin/settings/Discord");
    }

    [Fact]
    public async Task GetPreview_ShouldSendEveryRoleIdAsItsOwnQueryParameter()
    {
        var handler = new StubHttpMessageHandler(new RolePreviewDto(5, 2, 1));

        var preview = await new HttpRoleSettingsClient(handler.CreateClient()).GetPreviewAsync("Discord", [_statistik, _mitglied], Ct);

        preview.ShouldBe(new RolePreviewDto(5, 2, 1));
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe($"/api/admin/settings/Discord/preview?roleIds={_statistik}&roleIds={_mitglied}");
    }

    [Fact]
    public async Task GetHistory_ShouldAskForTheNewestEntries()
    {
        var change = new RoleSettingChangeDto(DateTimeOffset.UnixEpoch, "Anna", RoleSettingKind.Tracking, RoleSettingChangeAction.Added, "Statistik");
        var handler = new StubHttpMessageHandler(new[] { change });

        var history = await new HttpRoleSettingsClient(handler.CreateClient()).GetHistoryAsync("Discord", 20, Ct);

        history.ShouldBe([change]);
        handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe("/api/admin/settings/Discord/history?take=20");
    }

    [Fact]
    public async Task SetTrackingRole_ShouldPutTheRoleWithTheAntiforgeryHeader()
    {
        var handler = new StubHttpMessageHandler(_antiforgery).Respond("/api/admin/settings/Discord/tracking-role", HttpStatusCode.NoContent);

        await new HttpRoleSettingsClient(handler.CreateClient()).SetTrackingRoleAsync("Discord", _statistik, Ct);

        handler.Messages.Select(m => (m.Method, m.RequestUri!.AbsolutePath)).ShouldBe(
            [(HttpMethod.Get, "/auth/antiforgery"), (HttpMethod.Put, "/api/admin/settings/Discord/tracking-role")]);
        handler.Messages[1].Headers.GetValues("X-XSRF-TOKEN").ShouldBe(["request-token"]);
        Body<TrackingRoleRequest>(handler, 1).ShouldBe(new TrackingRoleRequest(_statistik));
    }

    [Fact]
    public async Task SetMemberAccessRoles_ShouldPutTheWholeSet()
    {
        var handler = new StubHttpMessageHandler(_antiforgery).Respond("/api/admin/settings/Discord/member-access-roles", HttpStatusCode.NoContent);

        await new HttpRoleSettingsClient(handler.CreateClient()).SetMemberAccessRolesAsync("Discord", [_statistik, _mitglied], Ct);

        handler.Messages.Select(m => m.Method).ShouldBe([HttpMethod.Get, HttpMethod.Put]);
        handler.Messages[1].Headers.GetValues("X-XSRF-TOKEN").ShouldBe(["request-token"]);
        Body<MemberAccessRolesRequest>(handler, 1).ShouldNotBeNull().RoleIds.ShouldBe([_statistik, _mitglied]);
    }

    [Fact]
    public async Task SetTrackingRole_WhenTheHostRejectsTheRole_ShouldThrowRoleSettingsRejected()
    {
        var handler = new StubHttpMessageHandler(_antiforgery)
            .Respond("/api/admin/settings/Discord/tracking-role", HttpStatusCode.BadRequest, _validationProblem);

        await Should.ThrowAsync<RoleSettingsRejectedException>(
            () => new HttpRoleSettingsClient(handler.CreateClient()).SetTrackingRoleAsync("Discord", _statistik, Ct));
    }

    [Fact]
    public async Task SetMemberAccessRoles_WhenTheHostRejectsARole_ShouldThrowRoleSettingsRejected()
    {
        var handler = new StubHttpMessageHandler(_antiforgery)
            .Respond("/api/admin/settings/Discord/member-access-roles", HttpStatusCode.BadRequest, _validationProblem);

        await Should.ThrowAsync<RoleSettingsRejectedException>(
            () => new HttpRoleSettingsClient(handler.CreateClient()).SetMemberAccessRolesAsync("Discord", [_statistik], Ct));
    }

    [Fact]
    public async Task SetTrackingRole_WhenTheHostAnswers400WithoutAValidationProblem_ShouldFailAsAnHttpError()
    {
        var handler = new StubHttpMessageHandler(_antiforgery).Respond("/api/admin/settings/Discord/tracking-role", HttpStatusCode.BadRequest);

        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => new HttpRoleSettingsClient(handler.CreateClient()).SetTrackingRoleAsync("Discord", _statistik, Ct));

        exception.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SetMemberAccessRoles_WhenTheHostAnswers400WithoutAValidationProblem_ShouldFailAsAnHttpError()
    {
        var handler = new StubHttpMessageHandler(_antiforgery).Respond("/api/admin/settings/Discord/member-access-roles", HttpStatusCode.BadRequest);

        await Should.ThrowAsync<HttpRequestException>(
            () => new HttpRoleSettingsClient(handler.CreateClient()).SetMemberAccessRolesAsync("Discord", [_statistik], Ct));
    }

    private static T? Body<T>(StubHttpMessageHandler handler, int index) =>
        JsonSerializer.Deserialize<T>(handler.Bodies[index]!, JsonSerializerOptions.Web);
}
