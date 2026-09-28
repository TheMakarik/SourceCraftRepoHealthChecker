using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceCraftRepoHealthChecker.Application.HealthCheck.UseCases;

public static class RepositoryReportJsonRenderer
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    public static byte[] Render(RepositoryAnalysis analysis) =>
        JsonSerializer.SerializeToUtf8Bytes(analysis, _options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
