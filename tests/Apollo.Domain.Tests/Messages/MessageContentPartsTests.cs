using Apollo.Domain.Messages;

namespace Apollo.Domain.Tests.Messages;

public sealed class MessageContentPartsTests
{
    [Theory]
    [InlineData("https://example.com/cat.png", MessageContentParts.Link)]
    [InlineData("  https://example.com  \n", MessageContentParts.Link)]
    [InlineData("<https://example.com>", MessageContentParts.Link)]
    [InlineData("https://example.com http://example.org/a?b=c", MessageContentParts.Link)]
    public void From_WhenMessageIsOnlyALink_ShouldBeLink(string content, MessageContentParts expected)
    {
        MessageContentParts.From(content).ShouldBe(expected);
    }

    [Theory]
    [InlineData("<@123>")]
    [InlineData("<@!123> <@456>")]
    [InlineData("<@&789>")]
    [InlineData("@everyone")]
    [InlineData("@here")]
    public void From_WhenMessageIsOnlyAMention_ShouldBeMention(string content)
    {
        MessageContentParts.From(content).ShouldBe(MessageContentParts.Mention);
    }

    [Fact]
    public void From_WhenMessageIsAMentionAndALink_ShouldBeMentionAndLink()
    {
        MessageContentParts.From("<@123> https://example.com").ShouldBe(MessageContentParts.Mention | MessageContentParts.Link);
    }

    [Fact]
    public void From_WhenMessageIsTextWithALink_ShouldBeTextAndLink()
    {
        MessageContentParts.From("look at this https://example.com").ShouldBe(MessageContentParts.Text | MessageContentParts.Link);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("?")]
    [InlineData("1")]
    public void From_WhenMessageIsOnlyText_ShouldBeText(string content)
    {
        MessageContentParts.From(content).ShouldBe(MessageContentParts.Text);
    }

    [Theory]
    [InlineData("😀")]
    [InlineData("😀 👍🏽 ❤️")]
    [InlineData("👨‍👩‍👧")]
    [InlineData("🇨🇭")]
    [InlineData("<:apollo:112233>")]
    [InlineData("<a:dance:445566> 😀")]
    public void From_WhenMessageIsOnlyEmoji_ShouldBeEmoji(string content)
    {
        MessageContentParts.From(content).ShouldBe(MessageContentParts.Emoji);
    }

    [Fact]
    public void From_WhenMessageIsAnAttachmentWithoutText_ShouldBeAttachment()
    {
        MessageContentParts.From("", attachmentCount: 2).ShouldBe(MessageContentParts.Attachment);
    }

    [Fact]
    public void From_WhenMessageIsAnAttachmentWithACaption_ShouldBeAttachmentAndText()
    {
        MessageContentParts.From("my cat", attachmentCount: 1).ShouldBe(MessageContentParts.Attachment | MessageContentParts.Text);
    }

    [Fact]
    public void From_WhenMessageIsAChannelMention_ShouldBeLink()
    {
        MessageContentParts.From("<#987>").ShouldBe(MessageContentParts.Link);
    }

    [Fact]
    public void From_WhenMessageIsEmpty_ShouldBeNone()
    {
        MessageContentParts.From(" ").ShouldBe(MessageContentParts.None);
    }

    [Fact]
    public void From_WhenMessageMixesEverything_ShouldSetEveryFlag()
    {
        MessageContentParts.From("hi <@1> <#2> 😀 https://example.com", attachmentCount: 1).ShouldBe(
            MessageContentParts.Text | MessageContentParts.Link | MessageContentParts.Mention
            | MessageContentParts.Emoji | MessageContentParts.Attachment);
    }
}
