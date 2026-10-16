using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformMessageUpdatedBuilder : PlatformEventBuilder<PlatformMessageUpdatedBuilder, PlatformMessageUpdated>
{
    public PlatformMessageUpdatedBuilder InChannel(PlatformRef channel) => With(e => e with { Channel = channel });

    public PlatformMessageUpdatedBuilder WithMessage(PlatformRef message) => With(e => e with { Message = message });

    public PlatformMessageUpdatedBuilder ByAuthor(PlatformRef? author) => With(e => e with { Author = author });

    public PlatformMessageUpdatedBuilder WithMetadata(MessageMetadata metadata) => With(e => e with { Metadata = metadata });

    protected override PlatformMessageUpdated CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: TestPlatform.Channel("general"),
        Message: TestPlatform.Message("message"),
        Author: TestPlatform.User("alice"),
        Metadata: MessageMetadata.Empty);
}
