using System.Reflection;
using Apollo.Platform.Abstractions.Commands;

namespace Apollo.Platform.Abstractions.Tests.Commands;

/// <summary>
/// The command model must fit platforms without interactions (Fluxer: text commands, DM replies), so it carries no
/// interaction tokens, deferral or ephemeral flags (docs/platform-parity.md, Command model).
/// </summary>
public sealed class CommandModelContractTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly string[] _interactionConcepts = ["Interaction", "Token", "Defer", "Ephemeral", "Flag", "Webhook"];

    private static readonly Type[] _commandTypes =
    [
        .. typeof(CommandInvocation).Assembly.GetExportedTypes()
            .Where(type => type.Namespace == typeof(CommandInvocation).Namespace)
            .OrderBy(type => type.Name, StringComparer.Ordinal),
    ];

    public static TheoryData<Type> CommandTypes => [.. _commandTypes];

    [Fact]
    public void CommandTypes_ShouldIncludeTheModelAndContracts()
    {
        _commandTypes.ShouldContain(typeof(CommandInvocation));
        _commandTypes.ShouldContain(typeof(ButtonInvocation));
        _commandTypes.ShouldContain(typeof(CommandReply));
        _commandTypes.ShouldContain(typeof(CommandDefinition));
        _commandTypes.ShouldContain(typeof(ICommandHandler));
        _commandTypes.ShouldContain(typeof(IButtonHandler));
        _commandTypes.ShouldContain(typeof(ICommandRouter));
    }

    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void CommandType_ShouldNameNoInteractionConcept(Type type)
    {
        var names = type.GetMembers(Declared)
            .Select(member => member.Name)
            .Concat(type.GetMethods(Declared).SelectMany(method => method.GetParameters())
                .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
                .Select(parameter => parameter.Name ?? string.Empty))
            .Append(type.Name);

        names.Where(name => _interactionConcepts.Any(concept => name.Contains(concept, StringComparison.OrdinalIgnoreCase)))
            .ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(CommandTypes))]
    public void CommandType_ShouldExposeOnlyNeutralTypes(Type type)
    {
        var exposed = type.GetProperties().Select(property => property.PropertyType)
            .Concat(type.GetMethods().SelectMany(method => method.GetParameters()).Select(parameter => parameter.ParameterType));

        exposed.Where(exposedType => exposedType.Namespace?.StartsWith("Apollo", StringComparison.Ordinal) == false
                && exposedType.Namespace != "System"
                && !exposedType.Namespace!.StartsWith("System.", StringComparison.Ordinal)
                && !exposedType.Namespace.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }
}
