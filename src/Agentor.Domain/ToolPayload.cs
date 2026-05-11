using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Agentor.Domain;

/// <summary>
/// Structured tool invocation envelope: JSON body plus optional schema/content-type metadata and a flat summary for stable traces and legacy bridges.
/// </summary>
[JsonConverter(typeof(ToolPayloadJsonConverter))]
public sealed class ToolPayload
{
    private readonly Dictionary<string, string> _summary;

    public ToolPayload(
        JsonObject? body,
        string? schemaId,
        string? contentType,
        IReadOnlyDictionary<string, string>? summary)
    {
        Body = body is null || body.Count == 0 ? new JsonObject() : CloneBody(body);
        SchemaId = schemaId;
        ContentType = contentType;
        _summary = summary is null || summary.Count == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(summary, StringComparer.OrdinalIgnoreCase);
    }

    public static ToolPayload Empty { get; } = new(new JsonObject(), null, null, null);

    public JsonObject Body { get; }

    public string? SchemaId { get; }

    public string? ContentType { get; }

    public IReadOnlyDictionary<string, string> Summary => _summary;

    public static ToolPayload FromLegacyDictionary(IReadOnlyDictionary<string, string>? legacy) =>
        new(new JsonObject(), null, null, legacy);

    /// <summary>Flat summary copy (legacy trace / HTTP DTO compat).</summary>
    public Dictionary<string, string> ToLegacySummary() => new(_summary, StringComparer.OrdinalIgnoreCase);

    /// <summary>Merges summary entries with scalar properties from <see cref="Body"/> root for policy evaluation.</summary>
    public Dictionary<string, string> ToPolicyEvaluationDictionary()
    {
        var d = ToLegacySummary();
        foreach (var pair in Body)
        {
            if (!TryScalarString(pair.Value, out var value))
            {
                continue;
            }

            d[pair.Key] = value;
        }

        return d;
    }

    public string ToPersistedJson(JsonSerializerOptions options)
    {
        var o = new JsonObject
        {
            ["body"] = CloneBody(Body),
            ["schemaId"] = SchemaId,
            ["contentType"] = ContentType,
            ["summary"] = JsonSerializer.SerializeToNode(_summary, options)
        };
        return o.ToJsonString(options);
    }

    public static ToolPayload FromPersistedJson(string json, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        var node = JsonNode.Parse(json);
        if (node is JsonObject obj && obj.TryGetPropertyValue("body", out var bodyNode) && bodyNode is JsonObject bodyObj)
        {
            var schemaId = ReadNullableString(obj["schemaId"]);
            var contentType = ReadNullableString(obj["contentType"]);
            var summary = ParseSummaryObject(obj["summary"] as JsonObject, options);
            return new ToolPayload(bodyObj, schemaId, contentType, summary);
        }

        var legacy = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
        return FromLegacyDictionary(legacy);
    }

    private static Dictionary<string, string>? ParseSummaryObject(JsonObject? summaryNode, JsonSerializerOptions options)
    {
        if (summaryNode is null || summaryNode.Count == 0)
        {
            return null;
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(summaryNode.ToJsonString(), options);
        return dict is null || dict.Count == 0 ? null : dict;
    }

    private static string? ReadNullableString(JsonNode? n)
    {
        if (n is null || n.GetValueKind() == JsonValueKind.Null)
        {
            return null;
        }

        return n.ToString();
    }

    private static JsonObject CloneBody(JsonObject body) =>
        JsonNode.Parse(body.ToJsonString())!.AsObject();

    private static bool TryScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is null)
        {
            return false;
        }

        switch (node.GetValueKind())
        {
            case JsonValueKind.String:
                value = node.GetValue<string>() ?? string.Empty;
                return true;
            case JsonValueKind.Number:
                value = node.ToJsonString();
                return true;
            case JsonValueKind.True:
                value = "true";
                return true;
            case JsonValueKind.False:
                value = "false";
                return true;
            default:
                return false;
        }
    }
}
