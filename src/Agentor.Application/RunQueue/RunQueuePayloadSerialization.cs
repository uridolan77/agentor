using System.Text.Json;
using Agentor.Domain;

namespace Agentor.Application.RunQueue;

/// <summary>Version marker for durable queue tool-input persistence (see <c>tool_payload_json</c> vs legacy <c>tool_input_json</c>).</summary>
public enum RunQueuePayloadVersion
{
    LegacyStringKeyed = 1,

    /// <summary>Persisted <see cref="ToolPayload"/> JSON (v2 shape: body, schemaId, contentType, summary).</summary>
    StructuredToolPayload = 2,
}

/// <summary>Serialize/deserialize structured tool payloads for the EF durable run queue store.</summary>
public static class RunQueuePayloadSerialization
{
    /// <remarks>Shared with API mapping for consistent deserialization.</remarks>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Persists structured input for the <c>tool_payload_json</c> column (ToolPayload v2 JSON).</summary>
    public static string SerializeStructuredColumn(ToolPayload payload) =>
        payload.ToPersistedJson(JsonOptions);

    /// <summary>Loads structured input from <c>tool_payload_json</c>; returns null when absent or whitespace.</summary>
    public static ToolPayload? TryDeserializeStructuredColumn(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return ToolPayload.FromPersistedJson(json, JsonOptions);
    }
}
