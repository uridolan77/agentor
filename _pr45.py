import json, pathlib
ROOT = pathlib.Path(r"c:/dev/agentor")

harness_path = ROOT / "tests/Agentor.Application.Tests/fixtures/eval/evaluation-harness-one-step-tool.json"
data = json.loads(harness_path.read_text(encoding="utf-8-sig"))
data["expectedSnapshot"]["externalAgentInvocationCompletedCount"] = 0
harness_path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

one_call = {
    "schemaVersion": 3,
    "kind": "RunEvaluationHarness",
    "agentName": "Eval",
    "objective": "obj",
    "traceId": "eval-external-agent-1",
    "recipeName": "one",
    "recipeVersion": "1",
    "toolStepId": "s1",
    "toolStepOrder": 1,
    "toolKey": "external-agent.invoke",
    "expectedSnapshot": {
        "runStatus": "Completed",
        "traceEventCount": 14,
        "toolCallCount": 1,
        "planStepCount": 1,
        "externalAgentInvocationCompletedCount": 1,
    },
}
(ROOT / "tests/Agentor.Application.Tests/fixtures/eval/external-agent-one-call.json").write_text(
    json.dumps(one_call, indent=2) + "\n",
    encoding="utf-8",
)

(ROOT / "src/Agentor.Application/Evaluation/RunEvaluationHarness.cs").write_text(
    """using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

public sealed record RunEvaluationSnapshot(
    AgentRunStatus RunStatus,
    int TraceEventCount,
    int ToolCallCount,
    int PlanStepCount,
    int ExternalAgentInvocationCompletedCount);

public static class RunEvaluationHarness
{
    public static async Task<RunEvaluationSnapshot> ExecutePlanAsync(
        IAgentPlanExecutor executor,
        AgentRun run,
        AgentPlan plan,
        CancellationToken cancellationToken)
    {
        _ = await executor.ExecuteAsync(run, plan, cancellationToken).ConfigureAwait(false);
        var toolCalls = run.Steps.Sum(s => s.ToolCalls.Count);
        var ext = run.Trace.Count(e => e.Kind == TraceEventKind.ExternalAgentInvocationCompleted);
        return new RunEvaluationSnapshot(run.Status, run.Trace.Count, toolCalls, plan.Steps.Count, ext);
    }
}
""",
    encoding="utf-8",
)

(ROOT / "src/Agentor.Application/Quality/RunQualityGateEvaluator.cs").write_text(
    """using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Quality;

public sealed record RunQualitySummary(
    bool Passed,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings);

public sealed record RunQualityGateOptions(bool WarnOnExternalAgentOutputUnreviewed = false);

public static class RunQualityGateEvaluator
{
    /// <summary>
    /// Evaluates minimal run-level quality signals. Optional <paramref name="plan"/> enables plan-shape warnings.
    /// </summary>
    public static RunQualitySummary Evaluate(
        AgentRun run,
        bool requireCompleted = true,
        AgentPlan? plan = null,
        RunQualityGateOptions? quality = null)
    {
        var violations = new List<string>();
        var warnings = new List<string>();

        if (requireCompleted && run.Status != AgentRunStatus.Completed)
        {
            if (run.Status == AgentRunStatus.Failed)
            {
                violations.Add("RUN_FAILED");
            }
            else if (run.Status == AgentRunStatus.RequiresReview)
            {
                violations.Add("RUN_REQUIRES_REVIEW");
            }
            else
            {
                violations.Add("RUN_NOT_COMPLETED");
            }
        }

        if (run.Status == AgentRunStatus.Completed
            && !run.Trace.Any(e => e.Kind == TraceEventKind.RunCompleted))
        {
            violations.Add("MISSING_RUN_COMPLETED_TRACE");
        }

        if (plan is not null
            && run.Status == AgentRunStatus.Completed
            && plan.Steps.Any(s => s.Status == AgentPlanStepStatus.Failed))
        {
            warnings.Add("COMPLETED_RUN_WITH_FAILED_PLAN_STEP");
        }

        if (quality?.WarnOnExternalAgentOutputUnreviewed == true
            && run.Trace.Any(e => e.Kind == TraceEventKind.ExternalAgentInvocationCompleted))
        {
            warnings.Add("EXTERNAL_AGENT_OUTPUT_UNREVIEWED");
        }

        return new RunQualitySummary(violations.Count == 0, violations, warnings);
    }
}
""",
    encoding="utf-8",
)

print("pr45 ok")
