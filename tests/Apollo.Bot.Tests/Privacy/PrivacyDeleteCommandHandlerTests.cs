using Apollo.Platform.Abstractions.Commands;
using Apollo.Testing.Data;
using static Apollo.Bot.Tests.Privacy.PrivacyCommandHost;
using static Apollo.Bot.Tests.Tracking.TrackingCommandHost;

namespace Apollo.Bot.Tests.Privacy;

public sealed class PrivacyDeleteCommandHandlerTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task Delete_ShouldReplyPrivatelyThatItIsPermanentWithAConfirmButtonAndDeleteNothing()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await RecordedData.SeedAsync(host, Alice, Bob);
        var before = await RecordedData.RowsAboutAsync(host, Alice);

        var reply = await host.RunAsync(Alice, DeletePath);

        reply.Visibility.ShouldBe(ReplyVisibility.Private);
        reply.Body().ShouldBe("Damit werden alle über dich erfassten Daten endgültig gelöscht. Das kann nicht rückgängig gemacht werden.");
        reply.Text.ShouldEndWith($"{PublicWebUrl}/datenschutz");
        var button = reply.Buttons.ShouldHaveSingleItem();
        button.Label.ShouldBe("Endgültig löschen");
        button.Style.ShouldBe(ButtonStyle.Danger);
        ButtonId.TryParse(button.Id, out var id).ShouldBeTrue();
        id.IsFor(Alice).ShouldBeTrue();
        (await RecordedData.RowsAboutAsync(host, Alice)).ShouldBe(before);
        host.Platform.RoleChanges.ShouldBeEmpty();
        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_WhenAnotherMemberClicksTheButton_ShouldDeleteNothingAndTurnThemAway()
    {
        await using var host = await CreatePrivacyHostInTrackingRoleModeAsync(Fixture);
        await RecordedData.SeedAsync(host, Alice, Bob);
        var before = await RecordedData.RowsAboutAsync(host, Alice);
        var prompt = await host.RunAsync(Alice, DeletePath);

        var reply = await host.Platform.ClickButtonAsync(Carol, prompt.Buttons.Single().Id, TestContext.Current.CancellationToken);

        reply.Body().ShouldBe("Dieser Button ist nicht für dich.");
        (await RecordedData.RowsAboutAsync(host, Alice)).ShouldBe(before);
        host.Platform.RoleChanges.ShouldBeEmpty();
        host.Gate.IsTracked(await host.UserIdAsync(Alice)).ShouldBeTrue();
    }
}
