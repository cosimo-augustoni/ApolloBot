using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformMessageCreatedBuilder : PlatformEventBuilder<PlatformMessageCreatedBuilder, PlatformMessageCreated>
{
    public PlatformMessageCreatedBuilder InChannel(PlatformRef channel) => With(e => e with { Channel = channel });

    public PlatformMessageCreatedBuilder WithMessage(PlatformRef message) => With(e => e with { Message = message });

    public PlatformMessageCreatedBuilder ByAuthor(PlatformRef author, bool isBot = false) => With(e => e with { Author = author, AuthorIsBot = isBot });

    public PlatformMessageCreatedBuilder ReplyingTo(PlatformRef message, PlatformRef? author = null) =>
        With(e => e with { ReplyToMessage = message, ReplyToAuthor = author });

    public PlatformMessageCreatedBuilder WithMetadata(MessageMetadata metadata) => With(e => e with { Metadata = metadata });

    protected override PlatformMessageCreated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: TestPlatform.Channel("general"),
        Message: TestPlatform.Message("message"),
        Author: TestPlatform.User("alice"),
        AuthorIsBot: false,
        ReplyToMessage: null,
        ReplyToAuthor: null,
        Metadata: MessageMetadata.Empty);
}
