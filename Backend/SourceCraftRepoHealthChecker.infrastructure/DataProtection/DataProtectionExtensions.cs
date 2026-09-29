using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SourceCraftRepoHealthChecker.infrastructure.DataProtection;

public static class DataProtectionExtensions
{
    public static IDataProtectionBuilder AddRepoHealthDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddDataProtection();

        var bucket = configuration["DataProtectionStorageOptions:Bucket"];
        if (!string.IsNullOrWhiteSpace(bucket))
        {
            builder.Services.AddSingleton<IXmlRepository>(provider => provider.GetRequiredService<S3XmlRepository>());
            return builder;
        }

        var keyPath = configuration["DataProtectionStorageOptions:KeyPath"];
        var directory = string.IsNullOrWhiteSpace(keyPath)
            ? Path.Join(Path.GetTempPath(), "srhc-dataprotection")
            : Environment.ExpandEnvironmentVariables(keyPath);
        builder.PersistKeysToFileSystem(new DirectoryInfo(directory));
        return builder;
    }
}
