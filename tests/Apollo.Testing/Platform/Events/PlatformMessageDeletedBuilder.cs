using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Testing.Platform;

public sealed class PlatformMessageDeletedBuilder : PlatformEventBuilder<PlatformMessageDeletedBuilder, PlatformMessageDeleted>
{
    public PlatformMessageDeletedBuilder InChannel(PlatformRef channel) => With(e => e with { Channel = channel });

    public PlatformMessageDeletedBuilder WithMessage(PlatformRef message) => With(e => e with { Message = message });

    public PlatformMessageDeletedBuilder ByAuthor(PlatformRef? author) => With(e => e with { Author = author });

    protected override PlatformMessageDeleted CreateDefault() => new(
        Guild: TestPlatform.Guild,
        OccurredAt: TestTime.Now,
        Channel: TestPlatform.Channel("general"),
        Message: TestPlatform.Message("message"),
        Author: null);
}
