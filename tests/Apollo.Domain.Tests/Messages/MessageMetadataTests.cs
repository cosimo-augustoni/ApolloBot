using Apollo.Domain.Messages;

namespace Apollo.Domain.Tests.Messages;

public sealed class MessageMetadataTests
{
    [Fact]
    public void From_WhenContentIsEmpty_ShouldHaveZeroCountsAndNoParts()
    {
        var metadata = MessageMetadata.From("", attachmentCount: 0, embedCount: 0);

        metadata.ShouldBe(MessageMetadata.Empty);
    }

    [Fact]
    public void From_ShouldCountLengthInCharactersWithAnEmojiAsOne()
    {
        MessageMetadata.From("hi 👍🏽", attachmentCount: 0, embedCount: 0).Length.ShouldBe(4);
    }

    [Theory]
    [InlineData("<:apollo:112233445566778899>", 1)]
    [InlineData("<a:dance:112233445566778899>", 1)]
    [InlineData("hi <@123456789012345678>", 4)]
    [InlineData("hi <@!123456789012345678>", 4)]
    [InlineData("<@&123456789012345678>", 1)]
    [InlineData("see <#123456789012345678>", 5)]
    [InlineData("https://example.com", 19)]
    [InlineData("@here", 5)]
    public void From_ShouldCountCustomEmojiAndMentionsAsOneCharacterAndLinksInFull(string content, int expected)
    {
        MessageMetadata.From(content, attachmentCount: 0, embedCount: 0).Length.ShouldBe(expected);
    }

    [Theory]
    [InlineData("hello world", 2)]
    [InlineData("  hello,   big\nworld!  ", 3)]
    [InlineData("hi <@1> look https://example.com 😀 <:apollo:5>", 2)]
    [InlineData("😀 👍", 0)]
    [InlineData("- 1 2", 2)]
    public void From_ShouldCountTheWordsOfTheTextPart(string content, int expected)
    {
        MessageMetadata.From(content, attachmentCount: 0, embedCount: 0).WordCount.ShouldBe(expected);
    }

    [Fact]
    public void From_ShouldCountUnicodeAndCustomEmoji()
    {
        var metadata = MessageMetadata.From("😀😀 👨‍👩‍👧 ❤️ 🇨🇭 <:apollo:1> <a:dance:2> 1 © :)", attachmentCount: 0, embedCount: 0);

        metadata.EmojiCount.ShouldBe(7);
    }

    [Fact]
    public void From_ShouldTakeAttachmentAndEmbedCountsAsGiven()
    {
        var metadata = MessageMetadata.From("https://example.com", attachmentCount: 3, embedCount: 1);

        metadata.AttachmentCount.ShouldBe(3);
        metadata.EmbedCount.ShouldBe(1);
    }

    [Fact]
    public void From_WhenPayloadLacksMentions_ShouldParseDistinctUserAndRoleMentionsAndEveryone()
    {
        var metadata = MessageMetadata.From("<@1> <@!1> <@2> <@&7> <@&7> @here", attachmentCount: 0, embedCount: 0);

        metadata.UserMentionCount.ShouldBe(2);
        metadata.RoleMentionCount.ShouldBe(1);
        metadata.MentionsEveryone.ShouldBeTrue();
        metadata.ContentParts.ShouldBe(MessageContentParts.Mention);
    }

    [Fact]
    public void From_WhenPayloadHasMentions_ShouldPreferThemOverParsing()
    {
        var metadata = MessageMetadata.From(
            "<@1> <@2> <@&7>",
            attachmentCount: 0,
            embedCount: 0,
            userMentionCount: 1,
            roleMentionCount: 0,
            mentionsEveryone: false);

        metadata.UserMentionCount.ShouldBe(1);
        metadata.RoleMentionCount.ShouldBe(0);
        metadata.MentionsEveryone.ShouldBeFalse();
    }

    [Fact]
    public void From_WhenEveryoneIsNotAMentionInThePayload_ShouldTreatItAsText()
    {
        var metadata = MessageMetadata.From("@everyone", attachmentCount: 0, embedCount: 0, userMentionCount: 0, roleMentionCount: 0, mentionsEveryone: false);

        metadata.MentionsEveryone.ShouldBeFalse();
        metadata.ContentParts.ShouldBe(MessageContentParts.Text);
    }

    [Fact]
    public void From_WhenPayloadReportsAMentionTheTextLacks_ShouldStillFlagMention()
    {
        var metadata = MessageMetadata.From("hello", attachmentCount: 0, embedCount: 0, userMentionCount: 1, roleMentionCount: 0, mentionsEveryone: false);

        metadata.ContentParts.ShouldBe(MessageContentParts.Text | MessageContentParts.Mention);
    }
}
