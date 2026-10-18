using Apollo.Data;
using Apollo.Stats.Raw;

namespace Apollo.Stats.Tests.Raw;

public sealed class RawStatsServiceValidationTests
{
    public static TheoryData<string> Methods() =>
        [nameof(IRawStatsService.GetVoiceBuddiesAsync), nameof(IRawStatsService.GetReplyGraphAsync), nameof(IRawStatsService.GetLongestVoiceSessionAsync), nameof(IRawStatsService.GetTopEmojisAsync), nameof(IRawStatsService.GetTopGamesAsync)];

    [Theory]
    [MemberData(nameof(Methods))]
    public async Task Get_WhenFromIsAfterTo_ShouldThrowAnArgumentException(string method) =>
        await Should.ThrowAsync<ArgumentException>(() => InvokeAsync(method, new RawStatsQuery(Guid.NewGuid(), null, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 14))));

    [Theory]
    [MemberData(nameof(Methods))]
    public async Task Get_WhenTopIsNotPositive_ShouldThrowAnArgumentOutOfRangeException(string method) =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => InvokeAsync(method, new RawStatsQuery(Guid.NewGuid(), null, new DateOnly(2026, 1, 14), new DateOnly(2026, 1, 14), Top: 0)));

    private static async Task InvokeAsync(string method, RawStatsQuery query)
    {
        // The context never connects: the query is rejected before it reaches the database.
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions("Host=unused"));
        IRawStatsService service = new RawStatsService(db);
        var ct = TestContext.Current.CancellationToken;
        Task task = method switch
        {
            nameof(IRawStatsService.GetVoiceBuddiesAsync) => service.GetVoiceBuddiesAsync(query, ct),
            nameof(IRawStatsService.GetReplyGraphAsync) => service.GetReplyGraphAsync(query, ct),
            nameof(IRawStatsService.GetLongestVoiceSessionAsync) => service.GetLongestVoiceSessionAsync(query, ct),
            nameof(IRawStatsService.GetTopEmojisAsync) => service.GetTopEmojisAsync(query, ct),
            _ => service.GetTopGamesAsync(query, ct),
        };
        await task;
    }
}
