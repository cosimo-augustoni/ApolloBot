using Apollo.Platform.Abstractions.Commands;
using NetCord;
using NetCord.Rest;
using ButtonStyle = Apollo.Platform.Abstractions.Commands.ButtonStyle;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordRepliesTests
{
    [Fact]
    public void ToMessage_WhenReplyIsPrivate_ShouldSetTheEphemeralFlag()
    {
        var message = DiscordReplies.ToMessage(CommandReply.Private("Hallo"));

        message.Flags.ShouldNotBeNull().HasFlag(MessageFlags.Ephemeral).ShouldBeTrue();
    }

    [Fact]
    public void ToMessage_WhenReplyIsPublic_ShouldNotSetTheEphemeralFlag()
    {
        var message = DiscordReplies.ToMessage(new CommandReply("Hallo", [], ReplyVisibility.Public));

        (message.Flags ?? default).HasFlag(MessageFlags.Ephemeral).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ReplyVisibility.Private)]
    [InlineData(ReplyVisibility.Public)]
    public void ToMessage_ShouldSuppressLinkPreviews(ReplyVisibility visibility)
    {
        var message = DiscordReplies.ToMessage(new CommandReply("Hallo", [], visibility));

        message.Flags.ShouldNotBeNull().HasFlag(MessageFlags.SuppressEmbeds).ShouldBeTrue();
    }

    [Fact]
    public void ToMessage_ShouldUseTheReplyTextAsContent()
    {
        DiscordReplies.ToMessage(CommandReply.Private("Hallo")).Content.ShouldBe("Hallo");
    }

    [Fact]
    public void ToMessage_WhenReplyHasNoButtons_ShouldHaveNoComponents()
    {
        (DiscordReplies.ToMessage(CommandReply.Private("Hallo")).Components ?? []).ShouldBeEmpty();
    }

    [Fact]
    public void ToMessage_WhenReplyHasButtons_ShouldRenderThemAsDiscordButtonsWithIdsAndLabels()
    {
        var reply = new CommandReply("Wirklich löschen?",
        [
            new ReplyButton("privacy-delete:42", "Löschen", ButtonStyle.Danger),
            new ReplyButton("cancel:42", "Abbrechen", ButtonStyle.Secondary),
        ]);

        var buttons = Buttons(DiscordReplies.ToMessage(reply).Components);

        buttons.Select(b => (b.CustomId, b.Label, b.Style)).ShouldBe(
        [
            ("privacy-delete:42", "Löschen", NetCord.ButtonStyle.Danger),
            ("cancel:42", "Abbrechen", NetCord.ButtonStyle.Secondary),
        ]);
    }

    [Theory]
    [InlineData(ButtonStyle.Primary, NetCord.ButtonStyle.Primary)]
    [InlineData(ButtonStyle.Secondary, NetCord.ButtonStyle.Secondary)]
    [InlineData(ButtonStyle.Success, NetCord.ButtonStyle.Success)]
    [InlineData(ButtonStyle.Danger, NetCord.ButtonStyle.Danger)]
    public void ToMessage_ShouldMapButtonStyles(ButtonStyle style, NetCord.ButtonStyle expected)
    {
        var reply = new CommandReply("Text", [new ReplyButton("x:1", "X", style)]);

        Buttons(DiscordReplies.ToMessage(reply).Components).ShouldHaveSingleItem().Style.ShouldBe(expected);
    }

    [Fact]
    public void ToMessage_WhenReplyHasMoreThanFiveButtons_ShouldSplitThemIntoRowsOfFive()
    {
        var reply = new CommandReply("Text", [.. Enumerable.Range(1, 7).Select(i => new ReplyButton($"x:{i}", $"{i}", ButtonStyle.Primary))]);

        var rows = DiscordReplies.ToMessage(reply).Components.ShouldNotBeNull().Cast<ActionRowProperties>().ToList();

        rows.Select(row => row.Components.Count()).ShouldBe([5, 2]);
    }

    private static List<ButtonProperties> Buttons(IEnumerable<IMessageComponentProperties>? components) =>
        [.. components.ShouldNotBeNull().Cast<ActionRowProperties>().SelectMany(row => row.Components).Cast<ButtonProperties>()];
}
