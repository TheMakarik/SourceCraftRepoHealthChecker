using System.Text.Json.Serialization;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

[JsonConverter(typeof(AppSecDefectGroupIdJsonConverter))]
public readonly record struct AppSecDefectGroupId(string Value);
