namespace Apollo.Platform.Abstractions.Tests;

public sealed class PlatformRefTests
{
    [Fact]
    public void Equals_WhenPlatformAndIdMatch_ShouldBeEqual()
    {
        new PlatformRef(PlatformKind.Discord, "1").ShouldBe(new PlatformRef(PlatformKind.Discord, "1"));
    }

    [Fact]
    public void Equals_WhenSameIdOnOtherPlatform_ShouldNotBeEqual()
    {
        new PlatformRef(PlatformKind.Discord, "1").ShouldNotBe(new PlatformRef(PlatformKind.Fluxer, "1"));
    }

    [Fact]
    public void Equals_WhenIdDiffers_ShouldNotBeEqual()
    {
        new PlatformRef(PlatformKind.Discord, "1").ShouldNotBe(new PlatformRef(PlatformKind.Discord, "2"));
    }

    [Fact]
    public void ToString_WhenCalled_ShouldCombinePlatformAndId()
    {
        new PlatformRef(PlatformKind.Fluxer, "42").ToString().ShouldBe("Fluxer:42");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenExternalIdIsBlank_ShouldThrow(string externalId)
    {
        Should.Throw<ArgumentException>(() => new PlatformRef(PlatformKind.Discord, externalId));
    }
}
