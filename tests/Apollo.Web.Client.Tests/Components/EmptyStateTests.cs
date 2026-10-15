using Apollo.Web.Client.Components;
using Bunit;

namespace Apollo.Web.Client.Tests.Components;

public sealed class EmptyStateTests : ApolloTestContext
{
    [Fact]
    public void EmptyState_ShouldRenderGivenTitleAndExplanation()
    {
        var empty = Render<EmptyState>(p => p
            .Add(e => e.Title, "Noch keine Daten")
            .Add(e => e.Explanation, "Sobald Apollo Aktivität erfasst, erscheint sie hier."));

        empty.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Noch keine Daten");
        empty.Find(".apollo-empty-explanation").TextContent.Trim().ShouldBe("Sobald Apollo Aktivität erfasst, erscheint sie hier.");
    }

    [Fact]
    public void EmptyState_WhenNothingIsPassed_ShouldRenderNoText()
    {
        var empty = Render<EmptyState>();

        empty.Find(".apollo-empty-state").TextContent.Trim().ShouldBeEmpty();
    }
}
