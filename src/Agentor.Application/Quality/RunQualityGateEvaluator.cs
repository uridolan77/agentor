using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Quality;

public sealed record RunQualitySummary(
    bool Passed,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings);

public static class RunQualityGateEvaluator
{
    /// <summary>
    /// Evaluates minimal run-level quality signals. Optional <paramref name="plan"/> enables plan-shape warnings.
    /// </summary>
    public static RunQualitySummary Evaluate(AgentRun run, bool requireCompleted = true, AgentPlan? plan = null)
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

        return new RunQualitySummary(violations.Count == 0, violations, warnings);
    }
}
