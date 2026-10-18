using System.Text.Json;
using Apollo.Stats.Raw;
using Apollo.Testing.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Stats.Tests.Raw;

public sealed class RawStatsRegistrationTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddRawStats_ShouldResolveTheServiceOnTheRuntimeRegistration()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z").Build());
        var builder = Fixture.CreateApplicationBuilder();
        builder.Services.AddRawStats();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        var longest = await scope.ServiceProvider.GetRequiredService<IRawStatsService>().GetLongestVoiceSessionAsync(seed.Query(), Ct);

        longest.ShouldNotBeNull().UserId.ShouldBe(seed.Alice);
    }

    [Fact]
    public void Results_ShouldRoundTripThroughJson()
    {
        // The recaps store the results as JSON (#46).
        var graph = new ReplyGraph([new ReplyEdge(Guid.NewGuid(), Guid.NewGuid(), 3)], [], []);
        var longest = new LongestVoiceSession(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch, TimeSpan.FromHours(3));

        var graphBack = JsonSerializer.Deserialize<ReplyGraph>(JsonSerializer.Serialize(graph)).ShouldNotBeNull();
        graphBack.Edges.ShouldBe(graph.Edges);
        JsonSerializer.Deserialize<LongestVoiceSession>(JsonSerializer.Serialize(longest)).ShouldBe(longest);
    }
}
