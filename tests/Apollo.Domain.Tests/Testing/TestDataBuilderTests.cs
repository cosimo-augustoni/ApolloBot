using Apollo.Testing;

namespace Apollo.Domain.Tests.Testing;

public sealed class TestDataBuilderTests
{
    [Fact]
    public void Build_WhenNoOverrides_ShouldUseDefaults()
    {
        var sample = new SampleBuilder().Build();

        sample.ShouldBe(new Sample("Alice", 30));
    }

    [Fact]
    public void Build_WhenOverridden_ShouldApplyOverride()
    {
        var sample = new SampleBuilder().Named("Bob").Build();

        sample.ShouldBe(new Sample("Bob", 30));
    }

    [Fact]
    public void Build_WhenOverriddenAdHoc_ShouldApplyOverridesInOrder()
    {
        var sample = new SampleBuilder().With(s => s with { Age = 40 }).With(s => s with { Age = s.Age + 1 }).Build();

        sample.ShouldBe(new Sample("Alice", 41));
    }

    [Fact]
    public void Build_WhenCalledTwice_ShouldReturnSeparateInstances()
    {
        var builder = new SampleBuilder();

        builder.Build().ShouldNotBeSameAs(builder.Build());
    }

    private sealed record Sample(string Name, int Age);

    private sealed class SampleBuilder : TestDataBuilder<SampleBuilder, Sample>
    {
        public SampleBuilder Named(string name) => With(s => s with { Name = name });

        protected override Sample CreateDefault() => new("Alice", 30);
    }
}
