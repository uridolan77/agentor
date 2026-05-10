using Agentor.Application.Coordination;
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
