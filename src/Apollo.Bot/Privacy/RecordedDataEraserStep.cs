namespace Apollo.Bot.Privacy;

internal sealed class RecordedDataEraserStep : IUserDataEraserStep
{
    public Task<IReadOnlyList<ErasedRows>> EraseAsync(Guid guildId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ErasedRows>>([]);
}
