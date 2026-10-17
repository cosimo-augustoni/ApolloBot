using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Privacy;

internal sealed class UserDataEraser : IUserDataEraser
{
    public Task<UserDataErasureOutcome> EraseAsync(PlatformRef guild, PlatformRef member, CancellationToken cancellationToken) =>
        Task.FromResult(UserDataErasureOutcome.Deleted);
}
