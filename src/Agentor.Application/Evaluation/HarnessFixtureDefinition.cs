using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

public sealed record HarnessExpectedSnapshot(
    string RunStatus,
    int TraceEventCount,
    int ToolCallCount,
    int PlanStepCount,
    int ExternalAgentInvocationCompletedCount);

/// <summary>
/// Parsed evaluation harness fixture (schema 2+). Registry entries point at JSON files of this shape.
/// </summary>
public sealed class HarnessFixtureDefinition
{
    public int SchemaVersion { get; init; }

    public string Kind { get; init; } = "";

    public string AgentName { get; init; } = "Eval";

    public string Objective { get; init; } = "obj";

    public string TraceId { get; init; } = "trace";

    public string RecipeName { get; init; } = "one";

    public string RecipeVersion { get; init; } = "1";

    public CoordinationTopology Topology { get; init; } = CoordinationTopology.SequentialPipeline;

    public string ToolStepId { get; init; } = "s1";

    public int ToolStepOrder { get; init; } = 1;

    public string ToolKey { get; init; } = "";

    public Dictionary<string, string>? ToolStepParameters { get; init; }

    public HarnessExpectedSnapshot? ExpectedSnapshot { get; init; }
}
