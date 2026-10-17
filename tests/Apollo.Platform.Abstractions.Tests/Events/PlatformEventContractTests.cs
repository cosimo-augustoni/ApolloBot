using System.Reflection;
using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Platform;

namespace Apollo.Platform.Abstractions.Tests.Events;

/// <summary>
/// Shape rules every neutral platform event must follow, checked by reflection so new events are covered too.
/// </summary>
public sealed class PlatformEventContractTests
{
    private static readonly Type[] _eventTypes =
    [
        .. typeof(PlatformEvent).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == typeof(PlatformEvent).Namespace && type != typeof(PlatformEvent))
            .OrderBy(type => type.Name, StringComparer.Ordinal),
    ];

    private static readonly NullabilityInfoContext _nullability = new();

    public static TheoryData<Type> EventTypes => [.. _eventTypes];

    [Fact]
    public void EventTypes_ShouldIncludeEveryEventM1Records()
    {
        _eventTypes.Select(type => type.Name).ShouldBe(
        [
            nameof(PlatformChannelCreated), nameof(PlatformChannelDeleted), nameof(PlatformChannelUpdated),
            nameof(PlatformConnectionStateChanged), nameof(PlatformGuildAvailable),
            nameof(PlatformMemberBanned), nameof(PlatformMemberJoined), nameof(PlatformMemberLeft),
            nameof(PlatformMemberUnbanned), nameof(PlatformMemberUpdated),
            nameof(PlatformMessageCreated), nameof(PlatformMessageDeleted), nameof(PlatformMessageUpdated),
            nameof(PlatformPresenceUpdated), nameof(PlatformReactionAdded), nameof(PlatformReactionRemoved),
            nameof(PlatformRoleCreated), nameof(PlatformRoleDeleted), nameof(PlatformRoleUpdated),
            nameof(PlatformVoiceStateChanged),
        ]);
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void Event_ShouldBeASealedRecordDerivedFromPlatformEvent(Type eventType)
    {
        eventType.IsSealed.ShouldBeTrue();
        eventType.IsAssignableTo(typeof(PlatformEvent)).ShouldBeTrue();
        eventType.GetMethod("<Clone>$").ShouldNotBeNull("events must be records");
    }

    [Fact]
    public void PlatformEvent_ShouldCarryGuildAndOccurredAt()
    {
        typeof(PlatformEvent).GetProperty(nameof(PlatformEvent.Guild))!.PropertyType.ShouldBe(typeof(PlatformRef));
        typeof(PlatformEvent).GetProperty(nameof(PlatformEvent.OccurredAt))!.PropertyType.ShouldBe(typeof(DateTimeOffset));
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void Event_ShouldReferenceUsersChannelsAndRolesOnlyAsPlatformRef(Type eventType)
    {
        var rawIds = ReachableProperties(eventType)
            .Where(property => property.Name.EndsWith("Id", StringComparison.Ordinal)
                && property.Name != nameof(PlatformVoiceState.ConnectionId))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}");

        rawIds.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(PlatformMessageCreated))]
    [InlineData(typeof(PlatformMessageUpdated))]
    [InlineData(typeof(PlatformMessageDeleted))]
    [InlineData(typeof(MessageMetadata))]
    public void MessageShape_ShouldExposeNoTextThatCouldCarryContent(Type type)
    {
        var textProperties = ReachableProperties(type)
            .Where(property => property.PropertyType == typeof(string) || property.PropertyType == typeof(char[]))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}");

        textProperties.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(typeof(PlatformMessageDeleted), nameof(PlatformMessageDeleted.Author))]
    [InlineData(typeof(PlatformMessageCreated), nameof(PlatformMessageCreated.ReplyToMessage))]
    [InlineData(typeof(PlatformMessageCreated), nameof(PlatformMessageCreated.ReplyToAuthor))]
    [InlineData(typeof(PlatformMessageUpdated), nameof(PlatformMessageUpdated.Author))]
    [InlineData(typeof(PlatformReactionAdded), nameof(PlatformReactionAdded.MessageAuthor))]
    [InlineData(typeof(PlatformReactionRemoved), nameof(PlatformReactionRemoved.MessageAuthor))]
    [InlineData(typeof(PlatformVoiceState), nameof(PlatformVoiceState.ConnectionId))]
    [InlineData(typeof(PlatformVoiceState), nameof(PlatformVoiceState.Channel))]
    public void OptionalReference_ShouldBeNullable(Type type, string propertyName)
    {
        var property = type.GetProperty(propertyName).ShouldNotBeNull();

        _nullability.Create(property).ReadState.ShouldBe(NullabilityState.Nullable);
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void Event_ShouldHaveATestBuilderWithTestDefaults(Type eventType)
    {
        var builderType = typeof(TestPlatform).Assembly.GetExportedTypes()
            .SingleOrDefault(type => type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(PlatformEventBuilder<,>)
                && baseType.GetGenericArguments()[1] == eventType);
        builderType.ShouldNotBeNull($"Apollo.Testing has no builder for {eventType.Name}");

        var built = (PlatformEvent)builderType.GetMethod("Build")!.Invoke(Activator.CreateInstance(builderType), null)!;

        built.ShouldBeOfType(eventType);
        built.Guild.ShouldBe(TestPlatform.Guild);
        built.OccurredAt.ShouldBe(TestTime.Now);
    }

    /// <summary>
    /// The properties of <paramref name="type"/> and, recursively, of the Apollo shapes it contains (incl. list items),
    /// stopping at <see cref="PlatformRef"/>.
    /// </summary>
    private static IEnumerable<PropertyInfo> ReachableProperties(Type type)
    {
        var visited = new HashSet<Type>();
        var pending = new Queue<Type>([type]);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var property in current.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name != "EqualityContract"))
            {
                yield return property;
                var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var shapeType = propertyType.IsGenericType ? propertyType.GetGenericArguments()[0] : propertyType;
                // PlatformRef is the allowed way to reference something, so its own ExternalId is not a raw id.
                if (shapeType != typeof(PlatformRef)
                    && shapeType.Namespace?.StartsWith("Apollo.", StringComparison.Ordinal) == true)
                {
                    pending.Enqueue(shapeType);
                }
            }
        }
    }
}
