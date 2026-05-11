using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Application.HumanReview;

/// <summary>
/// Continues a suspended multi-step plan after the blocked step was approved and executed.
/// </summary>
public sealed class PlanResumeOrchestrator(
    IToolRegistry toolRegistry,
    ReviewPolicyReevaluationService policyReevaluation,
    IToolExecutionPipeline toolExecutionPipeline,
    IAgentPlanExecutor planExecutor,
    IClock clock,
    ReviewTraceWriter traceWriter)
{
    /// <summary>
    /// Executes remaining plan steps from a cursor after the originally-blocked step has been approved and executed.
    /// Each step is fully policy-evaluated and run through the tool execution pipeline.
    /// The run is completed (or failed) on exit.
    /// </summary>
    public async Task ResumeRemainingPlanStepsAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        ToolPayload? approvedStepOutput,
        CancellationToken cancellationToken)
    {
        traceWriter.RecordMultiStepPlanResumed(run, cursor);

        var ctx = new PlanExecutionContext();
        foreach (var h in cursor.CompletedStepHistory)
        {
            ctx.History.Add(new PlanStepExecutionSnapshot(h.PlanStepId, h.SourceStepId, h.FinalStatus, h.ToolSucceeded, h.Output));
        }

        var approvedFlat = approvedStepOutput?.ToPolicyEvaluationDictionary();
        ctx.History.Add(new PlanStepExecutionSnapshot(
            cursor.BlockedAtPlanStepId,
            cursor.BlockedAtSourceStepId,
            AgentPlanStepStatus.Completed,
            approvedFlat is not null,
            approvedFlat));

        foreach (var pending in cursor.RemainingSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (run.Status != AgentRunStatus.Running)
            {
                return;
            }

            var continueLoop = await ExecutePendingResumeStepAsync(run, cursor, pending, ctx, cancellationToken);
            if (!continueLoop)
            {
                return;
            }
        }

        if (run.Status == AgentRunStatus.Running)
        {
            run.Complete(clock.UtcNow);
            traceWriter.RecordPlanExecutionCompletedAfterReview(run, cursor.PlanId);
        }
    }

    /// <summary>
    /// Executes one pending step from a resume cursor.
    /// Returns true to continue to the next step, false to stop (terminal failure or review suspension).
    /// </summary>
    private async Task<bool> ExecutePendingResumeStepAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        PendingPlanStep pending,
        PlanExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        if (pending.Kind == RecipeStepKind.Skill)
        {
            var stop = await planExecutor.ExecuteResumedSkillPlanStepAsync(run, cursor, pending, ctx, cancellationToken);
            return !stop && run.Status == AgentRunStatus.Running;
        }

        var input = BuildResumedStepInput(run, pending);

        if (!toolRegistry.TryGetRegistration(pending.ToolKey, out var registration) || registration is null)
        {
            run.Fail($"Unknown tool '{pending.ToolKey}' during plan resume.", clock.UtcNow);
            return false;
        }

        var runStep = run.StartStep($"PlanResume:{cursor.PlanId:N}:{pending.SourceStepId}", clock.UtcNow);

        var policyDecision = await policyReevaluation.EvaluateResumedPlanStepAsync(
            run,
            runStep.Id,
            pending.ToolKey,
            input,
            cancellationToken);

        runStep.AddPolicyDecision(policyDecision);
        traceWriter.RecordResumedStepPolicyEvaluated(run, cursor, pending, policyDecision);

        var toolCall = ToolCall.Start(run.Id, runStep.Id, pending.ToolKey, input, clock.UtcNow);

        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            if (pending.OnFailure == FailureHandlingPolicy.EscalateToReview)
            {
                toolCall.MarkRequiresReview(policyDecision.Reason, clock.UtcNow);
                runStep.AddToolCall(toolCall);
                runStep.MarkRequiresReview(clock.UtcNow);
                run.EnterRequiresReview(policyDecision.Reason, clock.UtcNow);
                ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.RequiresReview, false, null));
                RecordNewCursorForResumedStep(run, cursor, pending, ctx);
                return false;
            }

            toolCall.Deny(policyDecision.Reason, clock.UtcNow);
            runStep.AddToolCall(toolCall);
            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Failed, false, null));

            if (pending.OnFailure is FailureHandlingPolicy.ContinueOnFailure or FailureHandlingPolicy.MarkForCompensation)
            {
                runStep.Complete(clock.UtcNow);
                return true;
            }

            if (pending.OnFailure == FailureHandlingPolicy.SkipRemaining)
            {
                runStep.Complete(clock.UtcNow);
                return false;
            }

            runStep.Fail(clock.UtcNow);
            run.Fail(policyDecision.Reason, clock.UtcNow);
            return false;
        }

        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            toolCall.MarkRequiresReview(policyDecision.Reason, clock.UtcNow);
            runStep.AddToolCall(toolCall);
            runStep.MarkRequiresReview(clock.UtcNow);
            run.EnterRequiresReview(policyDecision.Reason, clock.UtcNow);
            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.RequiresReview, false, null));
            RecordNewCursorForResumedStep(run, cursor, pending, ctx);
            return false;
        }

        traceWriter.RecordResumedStepToolCallStarted(run, toolCall.Id, pending.ToolKey, pending.SourceStepId);

        runStep.AddToolCall(toolCall);

        var pipelineResult = await toolExecutionPipeline.ExecuteAsync(
            run,
            runStep.Id,
            toolCall.Id,
            registration.Executor,
            new ToolExecutionRequest(run.Id, runStep.Id, pending.ToolKey, ToolPayload.FromLegacyDictionary(input)),
            cancellationToken);

        if (pipelineResult.Success)
        {
            toolCall.Succeed(pipelineResult.Output!, clock.UtcNow);
            runStep.Complete(clock.UtcNow);

            var flatOutput = pipelineResult.Output!.ToPolicyEvaluationDictionary();
            IReadOnlyDictionary<string, string>? snapshotOutput = flatOutput;
            if (pending.OutputBinding is not null
                && flatOutput.TryGetValue(pending.OutputBinding.NormalizedKey, out var bound))
            {
                snapshotOutput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [pending.OutputBinding.NormalizedKey] = bound };
            }

            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Completed, true, snapshotOutput));
            traceWriter.RecordResumedPlanStepCompleted(run, cursor, pending, pipelineResult.AttemptsUsed);
            return true;
        }

        if (pending.OnFailure == FailureHandlingPolicy.EscalateToReview)
        {
            toolCall.MarkRequiresReview(pipelineResult.ErrorMessage ?? "Tool execution failed.", clock.UtcNow);
            runStep.MarkRequiresReview(clock.UtcNow);
            run.EnterRequiresReview(pipelineResult.ErrorMessage ?? "Tool execution failed.", clock.UtcNow);
            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.RequiresReview, false, null));
            RecordNewCursorForResumedStep(run, cursor, pending, ctx);
            return false;
        }

        toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", clock.UtcNow);
        ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Failed, false, null));

        if (pending.OnFailure is FailureHandlingPolicy.ContinueOnFailure or FailureHandlingPolicy.MarkForCompensation)
        {
            runStep.Complete(clock.UtcNow);
            return true;
        }

        if (pending.OnFailure == FailureHandlingPolicy.SkipRemaining)
        {
            runStep.Complete(clock.UtcNow);
            return false;
        }

        runStep.Fail(clock.UtcNow);
        run.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", clock.UtcNow);
        return false;
    }

    private void RecordNewCursorForResumedStep(AgentRun run, PlanResumeCursor originalCursor, PendingPlanStep blockedStep, PlanExecutionContext ctx)
    {
        var newRemaining = originalCursor.RemainingSteps
            .Where(s => s.OrderIndex > blockedStep.OrderIndex)
            .ToList();

        if (newRemaining.Count == 0)
        {
            return;
        }

        var newHistory = ctx.History
            .Select(h => new PlanStepResumeSnapshot(h.PlanStepId, h.SourceStepId, h.Status, h.ToolSucceeded, h.ToolOutput))
            .ToList();

        var blockedToolKey = blockedStep.Kind == RecipeStepKind.Skill
            ? (blockedStep.InvokedSkillKey ?? blockedStep.ToolKey)
            : blockedStep.ToolKey;

        var newCursor = new PlanResumeCursor(
            originalCursor.PlanId,
            blockedStep.PlanStepId,
            blockedStep.SourceStepId,
            blockedToolKey,
            newRemaining,
            newHistory,
            clock.UtcNow,
            SkillContinuation: null);

        run.RecordPlanResumeCursor(newCursor, clock.UtcNow);
    }

    private static IReadOnlyDictionary<string, string> BuildResumedStepInput(AgentRun run, PendingPlanStep pending)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["objective"] = run.Objective,
            ["agentName"] = run.AgentName
        };

        if (pending.StaticInputParameters is not null)
        {
            foreach (var kv in pending.StaticInputParameters)
            {
                d[kv.Key] = kv.Value;
            }
        }

        foreach (var kv in run.SessionMemory)
        {
            d["session:" + kv.Key] = kv.Value;
        }

        return d;
    }
}
