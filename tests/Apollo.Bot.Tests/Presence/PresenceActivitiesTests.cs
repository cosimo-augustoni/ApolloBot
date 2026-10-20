using Apollo.Bot.Presence;
using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
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

        PresenceActivities.From(presence).Select(a => a.Activity).ShouldBe([new PresenceActivity(expected, "Factorio")]);
    }

    [Fact]
    public void From_WhenUserHasACustomStatus_ShouldIgnoreIt()
    {
        var presence = new PlatformPresenceBuilder()
            .WithActivity(PlatformActivityType.Custom, "Custom Status")
            .Playing("Factorio")
            .Build();

        PresenceActivities.From(presence).Select(a => a.Activity).ShouldBe([new PresenceActivity(PresenceActivityType.Playing, "Factorio")]);
    }

    [Fact]
    public void From_WhenUserIsOffline_ShouldReturnNoActivities()
    {
        var presence = new PlatformPresenceBuilder().WithStatus(PlatformPresenceStatus.Offline).Playing("Factorio").Build();

        PresenceActivities.From(presence).ShouldBeEmpty();
    }
    [Fact]
    public void From_ShouldKeepTheRichPresence()
    {
        var start = TestTime.Now;
        var activity = new PlatformActivity(PlatformActivityType.Listening, "Spotify", start)
        {
            ReportedEnd = start.AddMinutes(3),
            Details = "Song 1",
            State = "Artist A",
            LargeText = "Album X",
            SmallText = "Small",
            PartyId = "party-1",
            PartySize = 1,
            PartyMax = 2,
            ApplicationId = "99",
        };

        PresenceActivities.From(new PlatformPresenceBuilder().WithActivity(activity).Build()).ShouldBe([
            new ObservedActivity(
                new PresenceActivity(PresenceActivityType.Listening, "Spotify"),
                new PresenceActivityDetails("Song 1", "Artist A", "Album X", "Small", "party-1", 1, 2, "99", start, start.AddMinutes(3))),
        ]);
    }

    [Fact]
    public void From_WhenATextIsLongerThan128Characters_ShouldCutIt()
    {
        var activity = new PlatformActivity(PlatformActivityType.Playing, "Factorio", null)
        {
            Details = new string('d', 200),
            State = new string('s', 129),
            LargeText = new string('l', 300),
            SmallText = new string('m', 128),
            PartyId = new string('p', 200),
        };

        var details = PresenceActivities.From(new PlatformPresenceBuilder().WithActivity(activity).Build()).ShouldHaveSingleItem().Details;

        details.ShouldBe(new PresenceActivityDetails(
            new string('d', 128), new string('s', 128), new string('l', 128), new string('m', 128), new string('p', 128)));
    }
}
