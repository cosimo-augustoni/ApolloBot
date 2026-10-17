using Apollo.Bot.Presence;
using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.Presence;

public sealed class PresenceActivitiesTests
{
    [Theory]
    [InlineData(PlatformActivityType.Playing, PresenceActivityType.Playing)]
    [InlineData(PlatformActivityType.Streaming, PresenceActivityType.Streaming)]
    [InlineData(PlatformActivityType.Listening, PresenceActivityType.Listening)]
    [InlineData(PlatformActivityType.Watching, PresenceActivityType.Watching)]
    [InlineData(PlatformActivityType.Competing, PresenceActivityType.Competing)]
    public void From_ShouldKeepTypeAndName(PlatformActivityType type, PresenceActivityType expected)
    {
        var presence = new PlatformPresenceBuilder().WithActivity(type, "Factorio").Build();

        PresenceActivities.From(presence).ShouldBe([new PresenceActivity(expected, "Factorio")]);
    }

    [Fact]
    public void From_WhenUserHasACustomStatus_ShouldIgnoreIt()
    {
        var presence = new PlatformPresenceBuilder()
            .WithActivity(PlatformActivityType.Custom, "Custom Status")
            .Playing("Factorio")
            .Build();

        PresenceActivities.From(presence).ShouldBe([new PresenceActivity(PresenceActivityType.Playing, "Factorio")]);
    }

    [Fact]
    public void From_WhenUserIsOffline_ShouldReturnNoActivities()
    {
        var presence = new PlatformPresenceBuilder().WithStatus(PlatformPresenceStatus.Offline).Playing("Factorio").Build();

        PresenceActivities.From(presence).ShouldBeEmpty();
    }
}
