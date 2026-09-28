using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceCraftRepoHealthChecker.infrastructure.SourceCraft;

public sealed class AppSecDefectGroupIdJsonConverter : JsonConverter<AppSecDefectGroupId>
{
    public override AppSecDefectGroupId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return new AppSecDefectGroupId(string.Empty);

        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;
        return new AppSecDefectGroupId(element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, AppSecDefectGroupId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
