using Xunit;

namespace Apollo.Testing.Data;

/// <summary>
/// The tests of one assembly that share a <see cref="TimescaleFixture"/>. They run one after another. Test classes that
/// need the container but no reset (e.g. they use <see cref="TimescaleFixture.NewDatabaseConnectionString"/>) opt in
/// with <c>[Collection&lt;TimescaleCollection&gt;]</c> instead of deriving from <see cref="TimescaleTest"/>.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TimescaleCollection : ICollectionFixture<TimescaleFixture>
{
    public const string Name = "Timescale";
}
