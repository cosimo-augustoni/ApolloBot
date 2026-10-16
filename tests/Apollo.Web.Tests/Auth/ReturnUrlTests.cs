using Apollo.Web.Auth;

namespace Apollo.Web.Tests.Auth;

public sealed class ReturnUrlTests
{
    [Theory]
    [InlineData("/", "/")]
    [InlineData("/me", "/me")]
    [InlineData("/admin/members?search=a#top", "/admin/members?search=a#top")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example/", "/")]
    [InlineData("me", "/")]
    [InlineData("javascript:alert(1)", "/")]
    [InlineData("/me\r\nSet-Cookie: x", "/")]
    public void LocalOrRoot_ShouldKeepOnlyLocalUrls(string? returnUrl, string expected)
    {
        ReturnUrl.LocalOrRoot(returnUrl).ShouldBe(expected);
    }
}
