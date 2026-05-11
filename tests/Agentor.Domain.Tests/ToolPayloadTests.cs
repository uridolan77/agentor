using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Agentor.Domain.Tests;

public sealed class ToolPayloadTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void FromLegacyDictionary_PutsAllKeysInSummary_EmptyBody()
    {
        var p = ToolPayload.FromLegacyDictionary(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "1",
            ["B"] = "2"
        });

        Assert.Empty(p.Body);
        Assert.Equal("1", p.Summary["a"]);
        Assert.Equal("2", p.Summary["B"]);
        Assert.Equal("1", p.ToLegacySummary()["a"]);
    }

    [Fact]
    public void ToPersistedJson_RoundTripsNestedBodyAndSummary()
    {
        var body = new JsonObject
        {
            ["nested"] = new JsonObject { ["count"] = 3 },
            ["top"] = "x"
        };
        var summary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["traceSlot"] = "stable" };
        var original = new ToolPayload(body, "schema-1", "application/json", summary);

        var json = original.ToPersistedJson(Options);
        var roundTrip = ToolPayload.FromPersistedJson(json, Options);

        Assert.Equal("schema-1", roundTrip.SchemaId);
        Assert.Equal("application/json", roundTrip.ContentType);
        Assert.Equal("stable", roundTrip.Summary["traceSlot"]);
        Assert.Equal(3, roundTrip.Body["nested"]!["count"]!.GetValue<int>());
        Assert.Equal("x", roundTrip.Body["top"]!.GetValue<string>());
    }

    [Fact]
    public void FromPersistedJson_LegacyFlatObject_LoadsAsSummaryOnly()
    {
        var legacyJson = """{"k":"v","n":"2"}""";
        var p = ToolPayload.FromPersistedJson(legacyJson, Options);

        Assert.Empty(p.Body);
        Assert.Equal("v", p.Summary["k"]);
        Assert.Equal("2", p.Summary["n"]);
    }

    [Fact]
    public void FromPersistedJson_EmptyObject_ReturnsEmptyPayload()
    {
        var p = ToolPayload.FromPersistedJson("{}", Options);

        Assert.Empty(p.Body);
        Assert.Empty(p.Summary);
        Assert.Null(p.SchemaId);
    }

    [Fact]
    public void FromPersistedJson_V2BodyArray_Malformed_DegradesToEmptyBody()
    {
        var json = """{"body":["not","object"],"schemaId":"s","summary":{}}""";
        var p = ToolPayload.FromPersistedJson(json, Options);

        Assert.Empty(p.Body);
        Assert.Equal("s", p.SchemaId);
    }

    [Fact]
    public void FromPersistedJson_V2MissingBody_ParsesSummaryAndMetadata()
    {
        var json = """{"schemaId":"my-schema","contentType":"application/json","summary":{"k":"v"}}""";
        var p = ToolPayload.FromPersistedJson(json, Options);

        Assert.Empty(p.Body);
        Assert.Equal("my-schema", p.SchemaId);
        Assert.Equal("application/json", p.ContentType);
        Assert.Equal("v", p.Summary["k"]);
    }

    [Fact]
    public void FromPersistedJson_InvalidLegacyJson_ReturnsEmpty()
    {
        var p = ToolPayload.FromPersistedJson("not-json", Options);

        Assert.Empty(p.Body);
        Assert.Empty(p.Summary);
    }

    [Fact]
    public void ToPolicyEvaluationDictionary_MergesSummaryWithScalarBodyProperties()
    {
        var body = new JsonObject
        {
            ["declaredCostUnits"] = 1.5m,
            ["nested"] = new JsonObject { ["x"] = 1 }
        };
        var p = new ToolPayload(body, null, null, new Dictionary<string, string> { ["summaryOnly"] = "yes" });

        var flat = p.ToPolicyEvaluationDictionary();

        Assert.Equal("yes", flat["summaryOnly"]);
        Assert.Equal("1.5", flat["declaredCostUnits"]);
        Assert.False(flat.ContainsKey("nested"));
    }
}
