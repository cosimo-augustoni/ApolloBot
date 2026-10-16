using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Apollo.Web.Tests;

/// <summary>A data protection key store in memory, for hosts without a database.</summary>
internal sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> _elements = [];

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_elements)
        {
            return [.. _elements.Select(e => new XElement(e))];
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_elements)
        {
            _elements.Add(new XElement(element));
        }
    }
}
