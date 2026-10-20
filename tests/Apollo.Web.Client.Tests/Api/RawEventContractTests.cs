using System.Reflection;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

/// <summary>
/// The raw event browser (#32) shows metadata only: message content is never stored (scope §2), so no DTO the browser
/// receives may even have a place for it.
/// </summary>
public sealed class RawEventContractTests
{
    private static readonly string[] _forbidden = ["content", "text", "body"];

    // The hover texts an app sends with its rich presence (#115): presence data the privacy page lists, not message content.
    private static readonly string[] _allowed = ["PresenceSegmentDetailsDto.LargeText", "PresenceSegmentDetailsDto.SmallText"];

    [Fact]
    public void EventDtos_ShouldContainNoContentField()
    {
        var properties = ReachableTypes(typeof(RawEventPageDto))
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
            .ToList();

        properties.ShouldContain("MessageEventDetailsDto.Length");
        properties.Except(_allowed).Where(p => _forbidden.Any(f => p[(p.IndexOf('.', StringComparison.Ordinal) + 1)..].Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ShouldBeEmpty();
    }

    /// <summary>The contract types reachable from <paramref name="root"/> through properties and collection elements.</summary>
    private static HashSet<Type> ReachableTypes(Type root)
    {
        var found = new HashSet<Type>();
        var pending = new Stack<Type>([root]);
        while (pending.TryPop(out var type))
        {
            var contract = Nullable.GetUnderlyingType(type) ?? type;
            if (contract.IsGenericType)
            {
                foreach (var argument in contract.GetGenericArguments())
                {
                    pending.Push(argument);
                }

                continue;
            }

            if (contract.Namespace != typeof(RawEventPageDto).Namespace || !found.Add(contract))
            {
                continue;
            }

            foreach (var property in contract.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                pending.Push(property.PropertyType);
            }
        }

        return found;
    }
}
