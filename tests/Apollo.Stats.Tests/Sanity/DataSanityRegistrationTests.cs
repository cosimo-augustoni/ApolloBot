using Apollo.Stats.Sanity;
using Apollo.Stats.Tests.Raw;
using Apollo.Testing.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Stats.Tests.Sanity;

public sealed class DataSanityRegistrationTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddDataSanity_ShouldResolveTheServiceOnTheRuntimeRegistration()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Message(seed.Alice, "2026-01-11T10:00:00Z").Build());
        var builder = Fixture.CreateApplicationBuilder();
        builder.Services.AddDataSanity();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        var report = await scope.ServiceProvider.GetRequiredService<IDataSanityService>().GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_WhenTheRangeEndsBeforeItStarts_ShouldThrow()
    {
        await using var db = Fixture.CreateDbContext();
        var query = new DataSanityQuery(Guid.NewGuid(), RawStatsSeed.Jan14, RawStatsSeed.Jan10);

        await Should.ThrowAsync<ArgumentException>(() => SanitySeed.Service(db).GetAsync(query, Ct));
    }
}
