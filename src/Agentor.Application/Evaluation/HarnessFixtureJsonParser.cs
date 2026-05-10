using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

/// <summary>
/// Parses versioned JSON fixtures used by <see cref="RunEvaluationHarness"/> and Phase 14 registry.
/// </summary>
public static class HarnessFixtureJsonParser
{
    private sealed class RootDto
    {
        public int SchemaVersion { get; set; }
        public string? Kind { get; set; }
        public string? AgentName { get; set; }
        public string? Objective { get; set; }
        public string? TraceId { get; set; }
        public string? RecipeName { get; set; }
        public string? RecipeVersion { get; set; }
        public string? CoordinationTopology { get; set; }
        public string? ToolStepId { get; set; }
        public int ToolStepOrder { get; set; } = 1;
        public string? ToolKey { get; set; }

        [JsonPropertyName("toolStepParameters")]
        public Dictionary<string, string>? ToolStepParameters { get; set; }

        public SnapshotDto? ExpectedSnapshot { get; set; }
    }

    private sealed class SnapshotDto
    {
        public string? RunStatus { get; set; }
        public int TraceEventCount { get; set; }
        public int ToolCallCount { get; set; }
        public int PlanStepCount { get; set; }
        public int ExternalAgentInvocationCompletedCount { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static HarnessFixtureDefinition Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<RootDto>(json, Options)
                  ?? throw new InvalidDataException("Harness fixture JSON deserialized to null.");

        if (dto.SchemaVersion < 2)
        {
            throw new InvalidDataException($"Harness fixture schemaVersion {dto.SchemaVersion} is not supported (minimum 2).");
        }

        if (!string.Equals(dto.Kind, "RunEvaluationHarness", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Harness fixture kind '{dto.Kind}' is not supported.");
        }

        CoordinationTopology topology = CoordinationTopology.SequentialPipeline;
        if (!string.IsNullOrWhiteSpace(dto.CoordinationTopology)
            && Enum.TryParse<CoordinationTopology>(dto.CoordinationTopology, ignoreCase: true, out var parsed))
        {
            topology = parsed;
        }

        HarnessExpectedSnapshot? snap = null;
        if (dto.ExpectedSnapshot is { } s && !string.IsNullOrWhiteSpace(s.RunStatus))
        {
            snap = new HarnessExpectedSnapshot(
                s.RunStatus,
                s.TraceEventCount,
                s.ToolCallCount,
                s.PlanStepCount,
                s.ExternalAgentInvocationCompletedCount);
        }

        return new HarnessFixtureDefinition
        {
            SchemaVersion = dto.SchemaVersion,
            Kind = dto.Kind ?? "",
            AgentName = dto.AgentName ?? "Eval",
            Objective = dto.Objective ?? "obj",
            TraceId = dto.TraceId ?? "trace",
            RecipeName = dto.RecipeName ?? "one",
            RecipeVersion = dto.RecipeVersion ?? "1",
            Topology = topology,
            ToolStepId = dto.ToolStepId ?? "s1",
            ToolStepOrder = dto.ToolStepOrder,
            ToolKey = dto.ToolKey ?? "",
            ToolStepParameters = dto.ToolStepParameters,
            ExpectedSnapshot = snap
        };
    }
}
