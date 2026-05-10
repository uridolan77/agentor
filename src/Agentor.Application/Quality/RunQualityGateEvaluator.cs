using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Quality;

public sealed record RunQualitySummary(bool Passed, IReadOnlyList<string> Violations);

public static class RunQualityGateEvaluator
{
    public static RunQualitySummary Evaluate(AgentRun run, bool requireCompleted = true)
    {
        var violations = new List<string>();
        if (requireCompleted && run.Status != AgentRunStatus.Completed)
        {
            violations.Add("RUN_NOT_COMPLETED");
        }

        if (run.Status == AgentRunStatus.Completed
            && !run.Trace.Any(e => e.Kind == TraceEventKind.RunCompleted))
        {
            violations.Add("MISSING_RUN_COMPLETED_TRACE");
        }

        return new RunQualitySummary(violations.Count == 0, violations);
    }
}