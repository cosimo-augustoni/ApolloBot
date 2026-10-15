using Xunit;

namespace Apollo.Testing.Data;

/// <summary>
/// Base class for tests against the shared TimescaleDB: puts the class into <see cref="TimescaleCollection"/> and
/// resets the database before every test.
/// </summary>
/// <example>
/// <code>
/// public sealed class VoiceSessionRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture)
/// {
///     [Fact]
///     public async Task Record_WhenUserLeaves_ShouldPersistSession()
///     {
///         await using var db = Fixture.CreateDbContext();
///         // ...
///     }
/// }
/// </code>
/// </example>
[Collection<TimescaleCollection>]
public abstract class TimescaleTest(TimescaleFixture fixture) : IAsyncLifetime
{
    protected TimescaleFixture Fixture { get; } = fixture;

    public virtual async ValueTask InitializeAsync() => await Fixture.ResetAsync(TestContext.Current.CancellationToken);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
