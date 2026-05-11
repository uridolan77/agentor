using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agentor.Domain;

public sealed class ToolPayloadJsonConverter : JsonConverter<ToolPayload>
{
    public override ToolPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ToolPayload.FromPersistedJson(doc.RootElement.GetRawText(), options);
    }

    public override void Write(Utf8JsonWriter writer, ToolPayload value, JsonSerializerOptions options)
    {
        writer.WriteRawValue(value.ToPersistedJson(options));
    }
}
