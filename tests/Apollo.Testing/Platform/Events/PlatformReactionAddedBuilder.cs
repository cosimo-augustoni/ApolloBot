using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformReactionAddedBuilder : PlatformEventBuilder<PlatformReactionAddedBuilder, PlatformReactionAdded>
{
    public PlatformReactionAddedBuilder InChannel(PlatformRef channel) => With(e => e with { Channel = channel });

    public PlatformReactionAddedBuilder OnMessage(PlatformRef message, PlatformRef? author = null) =>
        With(e => e with { Message = message, MessageAuthor = author });

    public PlatformReactionAddedBuilder ByUser(PlatformRef user) => With(e => e with { User = user });

    public PlatformReactionAddedBuilder WithEmoji(PlatformEmoji emoji) => With(e => e with { Emoji = emoji });

    protected override PlatformReactionAdded CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: TestPlatform.Channel("general"),
        Message: TestPlatform.Message("message"),
        User: TestPlatform.User("alice"),
        MessageAuthor: null,
        Emoji: new PlatformEmoji("👍", CustomEmoji: null));
}
