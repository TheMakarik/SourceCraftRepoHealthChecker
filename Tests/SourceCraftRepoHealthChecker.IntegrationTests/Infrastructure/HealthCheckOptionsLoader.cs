using System.Text.Json;
using SourceCraftRepoHealthChecker.Application.HealthCheck.Options;

namespace SourceCraftRepoHealthChecker.IntegrationTests.Infrastructure;

public static class HealthCheckOptionsLoader
{
    public static HealthCheckOptions Load()
    {
        var path = Path.Join(AppContext.BaseDirectory, "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("HealthCheckOptions", out var section))
            throw new InvalidOperationException("Секция HealthCheckOptions отсутствует в appsettings.json.");

        var options = JsonSerializer.Deserialize<HealthCheckOptions>(
            section.GetRawText(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return options ?? throw new InvalidOperationException("Не удалось прочитать HealthCheckOptions из appsettings.json.");
    }
}
