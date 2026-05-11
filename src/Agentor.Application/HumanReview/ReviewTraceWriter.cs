using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Application.HumanReview;

/// <summary>
/// Centralizes trace emission for human-review resume and multi-step plan continuation paths.
/// </summary>
public sealed class ReviewTraceWriter(IClock clock)
{
    public void RecordPostReviewPolicyEvaluated(AgentRun run, Guid stepId, PolicyDecision policyDecision)
    {
        run.RecordTrace(TraceEventKind.PolicyEvaluated, "Tool policy evaluated (post human review).", clock.UtcNow, new Dictionary<string, string>
        {
            ["stepId"] = stepId.ToString(),
            ["outcome"] = policyDecision.Outcome.ToString(),
            ["reasonCode"] = policyDecision.ReasonCode
        });
    }

    public void RecordPostReviewToolCallStarted(AgentRun run, Guid stepId, Guid toolCallId, string toolKey)
    {
        run.RecordTrace(TraceEventKind.ToolCallStarted, "Tool call started (after human review).", clock.UtcNow, new Dictionary<string, string>
        {
            ["stepId"] = stepId.ToString(),
            ["toolCallId"] = toolCallId.ToString(),
            ["toolKey"] = toolKey
        });
    }

    public void RecordToolCallCompletedAfterReview(
        AgentRun run,
        Guid toolCallId,
        ToolCallStatus status,
        int attemptsUsed,
        TimeSpan totalDuration)
    {
        run.RecordTrace(TraceEventKind.ToolCallCompleted, "Tool call completed.", clock.UtcNow, new Dictionary<string, string>
        {
            ["toolCallId"] = toolCallId.ToString(),
            ["status"] = status.ToString(),
            ["attemptsUsed"] = attemptsUsed.ToString(),
            ["totalDurationMs"] = ((long)totalDuration.TotalMilliseconds).ToString()
        });
    }

    public void RecordStepCompletedAfterReview(AgentRun run, Guid stepId)
    {
        run.RecordTrace(TraceEventKind.StepCompleted, "Step completed.", clock.UtcNow, new Dictionary<string, string>
        {
            ["stepId"] = stepId.ToString()
        });
    }

    public void RecordMultiStepPlanResumed(AgentRun run, PlanResumeCursor cursor)
    {
        run.RecordTrace(
            TraceEventKind.MultiStepPlanResumed,
            $"Multi-step plan resuming {cursor.RemainingSteps.Count} remaining step(s) after approval of '{cursor.BlockedAtSourceStepId}'.",
            clock.UtcNow,
            new Dictionary<string, string>
            {
                ["planId"] = cursor.PlanId.ToString("D"),
                ["blockedAtSourceStepId"] = cursor.BlockedAtSourceStepId,
                ["remainingSteps"] = cursor.RemainingSteps.Count.ToString()
            });
    }

    public void RecordPlanExecutionCompletedAfterReview(AgentRun run, Guid planId)
    {
        run.RecordTrace(
            TraceEventKind.PlanExecutionCompleted,
            "Multi-step plan execution completed after human review resume.",
            clock.UtcNow,
            new Dictionary<string, string> { ["planId"] = planId.ToString("D") });
    }

    public void RecordResumedStepPolicyEvaluated(
        AgentRun run,
        PlanResumeCursor cursor,
        PendingPlanStep pending,
        PolicyDecision policyDecision)
    {
        run.RecordTrace(
            TraceEventKind.PolicyEvaluated,
            $"Policy evaluated for resumed step '{pending.SourceStepId}'.",
            clock.UtcNow,
            new Dictionary<string, string>
            {
                ["planId"] = cursor.PlanId.ToString("D"),
                ["sourceStepId"] = pending.SourceStepId,
                ["toolKey"] = pending.ToolKey,
                ["outcome"] = policyDecision.Outcome.ToString()
            });
    }

    public void RecordResumedStepToolCallStarted(AgentRun run, Guid toolCallId, string toolKey, string sourceStepId)
    {
        run.RecordTrace(
            TraceEventKind.ToolCallStarted,
            $"Tool call started for resumed step '{sourceStepId}'.",
            clock.UtcNow,
            new Dictionary<string, string>
            {
                ["toolCallId"] = toolCallId.ToString(),
                ["toolKey"] = toolKey,
                ["sourceStepId"] = sourceStepId
            });
    }

    public void RecordResumedPlanStepCompleted(AgentRun run, PlanResumeCursor cursor, PendingPlanStep pending, int attemptsUsed)
    {
        run.RecordTrace(
            TraceEventKind.PlanExecutionStepCompleted,
            $"Resumed plan step completed: {pending.SourceStepId}.",
            clock.UtcNow,
            new Dictionary<string, string>
            {
                ["planId"] = cursor.PlanId.ToString("D"),
                ["sourceStepId"] = pending.SourceStepId,
                ["attemptsUsed"] = attemptsUsed.ToString()
            });
    }
}
