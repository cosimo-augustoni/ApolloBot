using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin.Events;
using Apollo.Web.Client.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>How the raw event browser (#32) names and describes one event row.</summary>
public sealed class DataEventTextTests : ApolloTestContext
{
    // 10:00 in Zurich.
    private static readonly DateTimeOffset _at = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    private IStringLocalizer<Strings> L => Services.GetRequiredService<IStringLocalizer<Strings>>();

    [Theory]
    [InlineData(DataEventType.Message, "Nachrichten", "message_events")]
    [InlineData(DataEventType.VoiceSession, "Voice-Sessions", "voice_sessions")]
    [InlineData(DataEventType.VoiceStateSegment, "Voice-Abschnitte", "voice_state_segments")]
    [InlineData(DataEventType.Reaction, "Reaktionen", "reaction_events")]
    [InlineData(DataEventType.Membership, "Mitgliedschaft", "membership_events")]
    [InlineData(DataEventType.PresenceActivity, "Aktivitäten", "presence_activity_sessions")]
    public void TableLabel_ShouldNameTheTableInGermanAndSql(DataEventType type, string label, string table)
    {
        DataEventText.TableLabel(L, type).ShouldBe(label);
        DataEventText.TableName(type).ShouldBe(table);
    }

    [Theory]
    [InlineData(DataEventType.Message, "Nachricht")]
    [InlineData(DataEventType.VoiceSession, "Voice-Session")]
    [InlineData(DataEventType.VoiceStateSegment, "Voice-Abschnitt")]
    [InlineData(DataEventType.Reaction, "Reaktion")]
    [InlineData(DataEventType.Membership, "Mitgliedschaft")]
    [InlineData(DataEventType.PresenceActivity, "Aktivität")]
    public void TypeName_ShouldNameOneRowInGerman(DataEventType type, string name)
    {
        DataEventText.TypeName(L, type).ShouldBe(name);
    }

    [Fact]
    public void TypeLabel_ShouldNameTheTypeAndKind()
    {
        DataEventText.TypeLabel(L, Message(Details() with { Kind = "Edit" })).ShouldBe("Nachricht · bearbeitet");
        DataEventText.TypeLabel(L, Reaction(new ReactionEventDetailsDto("Remove", "👍", false, null))).ShouldBe("Reaktion · entfernt");
        DataEventText.TypeLabel(L, Row(DataEventType.VoiceSession) with { VoiceSession = new(null, null) }).ShouldBe("Voice-Session");
        DataEventText.TypeLabel(L, Row(DataEventType.VoiceStateSegment) with { VoiceSegment = new(1, null, false, false, false, false) }).ShouldBe("Voice-Abschnitt");
        DataEventText.TypeLabel(L, Row(DataEventType.Membership) with { Membership = new("Ban") }).ShouldBe("Mitgliedschaft · gebannt");
        DataEventText.TypeLabel(L, Row(DataEventType.PresenceActivity) with { PresenceActivity = new("Listening", "Spotify", _at) }).ShouldBe("Aktivität · hört");
    }

    [Fact]
    public void TypeLabel_WhenKindUnknown_ShouldShowTheStoredValue()
    {
        DataEventText.TypeLabel(L, Row(DataEventType.Membership) with { Membership = new("Timeout") }).ShouldBe("Mitgliedschaft · Timeout");
    }

    [Fact]
    public void Details_WhenMessage_ShouldListTheMetadata()
    {
        var text = DataEventText.Details(L, Message(Details() with { ReplyTo = MockDataOverviewClient.Anna }));

        text.ShouldBe("42 Zeichen · 8 Wörter · 1 Anhang · 1 Emoji · Text, Anhang · Antwort an Anna");
    }

    [Fact]
    public void Details_WhenMessageHasMoreOfEach_ShouldUsePlurals()
    {
        var text = DataEventText.Details(L, Message(Details() with
        {
            WordCount = 1,
            AttachmentCount = 2,
            EmbedCount = 1,
            EmojiCount = 3,
            UserMentionCount = 2,
            RoleMentionCount = 1,
            MentionsEveryone = true,
            Parts = [],
            ThreadParent = MockDataOverviewClient.Allgemein,
            IsBot = true,
        }));

        text.ShouldBe("42 Zeichen · 1 Wort · 2 Anhänge · 1 Embed · 3 Emojis · 3 Erwähnungen · @everyone · Thread in allgemein · Bot");
    }

    [Fact]
    public void Details_WhenMessageDeleted_ShouldShowThePlaceholder()
    {
        var text = DataEventText.Details(L, Message(new MessageEventDetailsDto("Delete", null, null, null, null, null, null, null, null, null, null, null, false)));

        text.ShouldBe("–");
    }

    [Fact]
    public void Details_WhenReactionAuthorUntracked_ShouldSayNichtErfasst()
    {
        DataEventText.Details(L, Reaction(new ReactionEventDetailsDto("Add", "👍", false, null))).ShouldBe("👍 · auf Nachricht von Nicht erfasst");
        DataEventText.Details(L, Reaction(new ReactionEventDetailsDto("Add", "lachen", true, MockDataOverviewClient.Ben))).ShouldBe(":lachen: · auf Nachricht von Ben");
    }

    [Fact]
    public void Details_WhenSessions_ShouldShowEndOrOpen()
    {
        DataEventText.Details(L, Row(DataEventType.VoiceSession) with { VoiceSession = new(null, null) }).ShouldBe("offen");
        DataEventText.Details(L, Row(DataEventType.VoiceSession) with { VoiceSession = new(_at.AddMinutes(95), "Unobserved") })
            .ShouldBe("bis 11:35 · Ende nicht beobachtet");
        DataEventText.Details(L, Row(DataEventType.VoiceStateSegment) with { VoiceSegment = new(12, null, true, true, true, true) })
            .ShouldBe("Session 12 · stumm · taub · Stream · Kamera · offen");
        DataEventText.Details(L, Row(DataEventType.VoiceStateSegment) with { VoiceSegment = new(12, _at.AddMinutes(5), false, false, false, false) })
            .ShouldBe("Session 12 · bis 10:05");
        DataEventText.Details(L, Row(DataEventType.PresenceActivity) with { PresenceActivity = new("Playing", "Factorio", _at.AddHours(2)) })
            .ShouldBe("Factorio · bis 12:00");
        DataEventText.Details(L, Row(DataEventType.Membership) with { Membership = new("Join") }).ShouldBe("–");
    }

    [Fact]
    public void Names_WhenMissing_ShouldShowPlaceholders()
    {
        DataEventText.UserName(L, new EventUserDto(Guid.NewGuid(), null)).ShouldBe("Unbekannt");
        DataEventText.UserName(L, MockDataOverviewClient.Anna).ShouldBe("Anna");
        DataEventText.ChannelName(L, null).ShouldBe("–");
        DataEventText.ChannelName(L, new EventChannelDto(Guid.NewGuid(), null)).ShouldBe("Unbekannt");
        DataEventText.ChannelName(L, MockDataOverviewClient.Memes).ShouldBe("memes");
    }

    private static MessageEventDetailsDto Details() =>
        new("Create", 42, 8, 1, 0, 1, 0, 0, false, ["Text", "Attachment"], null, null, false);

    private static RawEventDto Message(MessageEventDetailsDto details) => Row(DataEventType.Message) with { Message = details };

    private static RawEventDto Reaction(ReactionEventDetailsDto details) => Row(DataEventType.Reaction) with { Reaction = details };

    private static RawEventDto Row(DataEventType type) =>
        new(1, type, _at, MockDataOverviewClient.Anna, MockDataOverviewClient.Allgemein);
}
