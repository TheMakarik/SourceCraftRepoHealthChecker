using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace SourceCraftRepoHealthChecker.infrastructure.DataProtection;

public sealed class LocalFileXmlRepository(string directory) : IXmlRepository
{
    private const string RootElementName = "keys";
    private const string FileName = "keys.xml";
    private readonly object _sync = new();

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_sync)
        {
            var container = Read();
            return container?.Elements().ToArray() ?? [];
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_sync)
        {
            var container = Read() ?? new XElement(RootElementName);
            container.Add(element);
            Write(container);
        }
    }

    private XElement? Read()
    {
        var path = Path.Join(directory, FileName);
        if (!File.Exists(path))
            return null;

        return XElement.Load(path);
    }

    private void Write(XElement container)
    {
        Directory.CreateDirectory(directory);
        var bytes = Encoding.UTF8.GetBytes(container.ToString(SaveOptions.DisableFormatting));
        File.WriteAllBytes(Path.Join(directory, FileName), bytes);
    }
}
