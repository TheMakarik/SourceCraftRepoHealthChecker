using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SourceCraftRepoHealthChecker.infrastructure.Options;

namespace SourceCraftRepoHealthChecker.infrastructure.DataProtection;

public sealed class S3XmlRepository(IMinioClient minio, IOptions<DataProtectionStorageOptions> options) : IXmlRepository
{
    private const string RootElementName = "keys";
    private const string ObjectName = "keys.xml";

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var container = ReadAsync().GetAwaiter().GetResult();
        return container?.Elements().ToArray() ?? [];
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var container = ReadAsync().GetAwaiter().GetResult() ?? new XElement(RootElementName);
        container.Add(element);

        var bytes = Encoding.UTF8.GetBytes(container.ToString(SaveOptions.DisableFormatting));
        using var stream = new MemoryStream(bytes);

        minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(options.Value.Bucket)
            .WithObject(options.Value.Prefix + ObjectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType("application/xml"))
            .GetAwaiter().GetResult();
    }

    private async Task<XElement?> ReadAsync()
    {
        var settings = options.Value;
        if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(settings.Bucket)))
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(settings.Bucket));

        var content = new MemoryStream();
        try
        {
            await minio.GetObjectAsync(new GetObjectArgs()
                .WithBucket(settings.Bucket)
                .WithObject(settings.Prefix + ObjectName)
                .WithCallbackStream(stream => stream.CopyTo(content)));
        }
        catch (Minio.Exceptions.MinioException)
        {
            return null;
        }

        if (content.Length == 0)
            return null;

        content.Position = 0;
        return XElement.Load(content);
    }
}
