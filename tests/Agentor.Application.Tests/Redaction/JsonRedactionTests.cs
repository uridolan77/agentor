using System.Text.Json.Nodes;
using Agentor.Application.Options;
using Agentor.Application.Redaction;
using Xunit;

namespace Agentor.Application.Tests.Redaction;

public sealed class JsonRedactionTests
{
    [Fact]
    public void Apply_RedactsMatchingKeys_AndRecordsPaths()
    {
        var json = "{\"safe\":\"ok\",\"myApiKey\":\"leak\",\"nested\":{\"password\":\"x\"},\"items\":[{\"token\":\"t\"}]}";
        var node = JsonNode.Parse(json)!;
        var policy = new RedactionPolicy(["apiKey", "secret", "password", "token"]);

        var result = JsonRedaction.Apply(node, policy);

        Assert.True(result.RedactedPropertyCount >= 3);
        Assert.Contains("/myApiKey", result.RedactedKeyPaths, StringComparer.Ordinal);
        Assert.Contains("/nested/password", result.RedactedKeyPaths, StringComparer.Ordinal);
        Assert.Contains("/items/0/token", result.RedactedKeyPaths, StringComparer.Ordinal);

        var text = node.ToJsonString();
        Assert.DoesNotContain("leak", text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", text, StringComparison.Ordinal);
        Assert.Contains("\"safe\":\"ok\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveFieldCatalog_MergeWithConfigured_IncludesDefaultsAndExtras()
    {
        var merged = SensitiveFieldCatalog.MergeWithConfigured(["customSecret"]);
        Assert.Contains("apiKey", merged, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("customSecret", merged, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromAuditExportOptions_MergesConfiguredList()
    {
        var opts = new AuditExportOptions { SensitiveKeySubstrings = ["customSecret"] };
        var policy = RedactionPolicy.FromAuditExportOptions(opts);
        Assert.Contains("customSecret", policy.KeyNameSubstrings, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("authorization", policy.KeyNameSubstrings, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_RedactsSubstringMatch_InMiddleOfPropertyName()
    {
        var json = "{\"x_apiKey_y\":\"secret\",\"plain\":\"keep\"}";
        var node = JsonNode.Parse(json)!;
        var policy = new RedactionPolicy(["apikey"]);

        var result = JsonRedaction.Apply(node, policy);

        Assert.Contains("/x_apiKey_y", result.RedactedKeyPaths, StringComparer.Ordinal);
        var text = node.ToJsonString();
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.Contains("\"plain\":\"keep\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_RedactsEachObject_InArray()
    {
        var json = "{\"items\":[{\"token\":\"a\"},{\"token\":\"b\"}],\"ok\":1}";
        var node = JsonNode.Parse(json)!;
        var policy = new RedactionPolicy(["token"]);

        var result = JsonRedaction.Apply(node, policy);

        Assert.Contains("/items/0/token", result.RedactedKeyPaths, StringComparer.Ordinal);
        Assert.Contains("/items/1/token", result.RedactedKeyPaths, StringComparer.Ordinal);
        Assert.Equal(2, result.RedactedPropertyCount);
        var text = node.ToJsonString();
        Assert.DoesNotContain("a", text, StringComparison.Ordinal);
        Assert.DoesNotContain("b", text, StringComparison.Ordinal);
        Assert.Contains("\"ok\":1", text, StringComparison.Ordinal);
    }
}