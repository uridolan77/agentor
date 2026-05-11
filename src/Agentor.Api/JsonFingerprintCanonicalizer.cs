using System.Linq;
using System.Text;
using System.Text.Json;

namespace Agentor.Api;

/// <summary>
/// Produces stable JSON text for idempotency fingerprints: object property names are sorted lexicographically
/// so semantically equivalent payloads hash the same regardless of property order in the request body.
/// </summary>
public static class JsonFingerprintCanonicalizer
{
    public static string Canonicalize(JsonElement element)
    {
        try
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonical(writer, element);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return element.GetRawText();
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonical(writer, prop.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
