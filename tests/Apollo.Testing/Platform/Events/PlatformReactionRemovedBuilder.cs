using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Testing.Platform;

public sealed class PlatformReactionRemovedBuilder : PlatformEventBuilder<PlatformReactionRemovedBuilder, PlatformReactionRemoved>
{
    public PlatformReactionRemovedBuilder InChannel(PlatformRef channel) => With(e => e with { Channel = channel });

    public PlatformReactionRemovedBuilder OnMessage(PlatformRef message, PlatformRef? author = null) =>
        With(e => e with { Message = message, MessageAuthor = author });

    public PlatformReactionRemovedBuilder ByUser(PlatformRef user) => With(e => e with { User = user });

    public PlatformReactionRemovedBuilder WithEmoji(PlatformEmoji emoji) => With(e => e with { Emoji = emoji });

    protected override PlatformReactionRemoved CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: TestPlatform.Channel("general"),
        Message: TestPlatform.Message("message"),
        User: TestPlatform.User("alice"),
        MessageAuthor: null,
        Emoji: new PlatformEmoji("👍", CustomEmoji: null));
}
