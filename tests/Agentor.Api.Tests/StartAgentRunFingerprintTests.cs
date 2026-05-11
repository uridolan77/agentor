using System.Text.Json;
using Agentor.Api;
using Agentor.Contracts;
using Agentor.Domain.Enums;

namespace Agentor.Api.Tests;

public sealed class StartAgentRunFingerprintTests
{
    private static StartAgentRunRequestDto BaseDto(JsonElement? toolInputPayload = null) =>
        new(
            AgentName: "Agent",
            Objective: "Objective",
            TraceId: null,
            TenantId: null,
            WorkspaceId: null,
            ProjectId: null,
            KnowledgeScopeId: null,
            Mode: RunExecutionMode.LegacyFakeTool,
            RecipeId: null,
            PlanId: null,
            ToolKey: "t1",
            SkillKey: null,
            Input: null,
            ToolInputPayload: toolInputPayload);

    private static JsonElement Payload(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Compute_ToolInputPayload_PropertyOrderInsensitive()
    {
        var a = BaseDto(Payload("""{"z":1,"a":2}"""));
        var b = BaseDto(Payload("""{"a":2,"z":1}"""));

        Assert.Equal(StartAgentRunFingerprint.Compute(a, false, null), StartAgentRunFingerprint.Compute(b, false, null));
    }

    [Fact]
    public void Compute_ToolInputPayload_WhitespaceInsensitive()
    {
        var compact = BaseDto(Payload("""{"a":1}"""));
        var spaced = BaseDto(Payload("""
            {
              "a": 1
            }
            """));

        Assert.Equal(StartAgentRunFingerprint.Compute(compact, false, null), StartAgentRunFingerprint.Compute(spaced, false, null));
    }

    [Fact]
    public void JsonFingerprintCanonicalizer_SortsNestedObjectKeys()
    {
        using var doc = JsonDocument.Parse("""{"outer":{"b":1,"a":2}}""");
        var canon = JsonFingerprintCanonicalizer.Canonicalize(doc.RootElement);
        Assert.Equal("""{"outer":{"a":2,"b":1}}""", canon);
    }
}
