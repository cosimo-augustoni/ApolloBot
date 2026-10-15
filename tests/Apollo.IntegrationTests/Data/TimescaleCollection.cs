namespace Apollo.IntegrationTests.Data;

[CollectionDefinition(Name)]
public sealed class TimescaleCollection : ICollectionFixture<TimescaleContainerFixture>
{
    public const string Name = "Timescale";
}
