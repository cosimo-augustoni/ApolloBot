using Apollo.Platform.Abstractions.Commands;
using Apollo.Testing.Platform;

namespace Apollo.Platform.Abstractions.Tests.Commands;

public sealed class ButtonIdTests
{
    [Fact]
    public void ToString_WhenNoArgument_ShouldJoinPrefixAndOwner()
    {
        var id = ButtonId.For("privacy-delete", TestPlatform.User("42"));

        id.ToString().ShouldBe("privacy-delete:42");
    }

    [Fact]
    public void ToString_WhenArgument_ShouldAppendIt()
    {
        var id = ButtonId.For("privacy-delete", TestPlatform.User("42"), "confirm");

        id.ToString().ShouldBe("privacy-delete:42:confirm");
    }

    [Theory]
    [InlineData("privacy-delete:42", "privacy-delete", "42", null)]
    [InlineData("privacy-delete:42:confirm", "privacy-delete", "42", "confirm")]
    [InlineData("privacy-delete:42:a:b", "privacy-delete", "42", "a:b")]
    public void TryParse_WhenWellFormed_ShouldReturnTheParts(string value, string prefix, string owner, string? argument)
    {
        ButtonId.TryParse(value, out var id).ShouldBeTrue();

        id.ShouldBe(new ButtonId(prefix, owner, argument));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("privacy-delete")]
    [InlineData(":42")]
    [InlineData("privacy-delete:")]
    [InlineData("privacy-delete::confirm")]
    public void TryParse_WhenMalformed_ShouldReturnFalse(string? value)
    {
        ButtonId.TryParse(value, out var id).ShouldBeFalse();

        id.ShouldBeNull();
    }

    [Fact]
    public void IsFor_WhenMemberIsTheOwner_ShouldReturnTrue()
    {
        ButtonId.For("x", TestPlatform.User("42")).IsFor(TestPlatform.User("42")).ShouldBeTrue();
    }

    [Fact]
    public void IsFor_WhenMemberIsSomeoneElse_ShouldReturnFalse()
    {
        ButtonId.For("x", TestPlatform.User("42")).IsFor(TestPlatform.User("43")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("a:b", "42")]
    [InlineData("", "42")]
    [InlineData("x", "4:2")]
    [InlineData("x", " ")]
    public void New_WhenPrefixOrOwnerIsBlankOrContainsTheSeparator_ShouldThrow(string prefix, string owner)
    {
        Should.Throw<ArgumentException>(() => new ButtonId(prefix, owner));
    }

    [Fact]
    public void ToString_WhenLongerThanMaxLength_ShouldThrow()
    {
        var id = new ButtonId("x", "42", new string('a', ButtonId.MaxLength - 4));

        Should.Throw<InvalidOperationException>(id.ToString);
    }

    [Fact]
    public void ToString_WhenExactlyMaxLength_ShouldReturnTheId()
    {
        var id = new ButtonId("x", "42", new string('a', ButtonId.MaxLength - 5));

        id.ToString().Length.ShouldBe(ButtonId.MaxLength);
    }
}
