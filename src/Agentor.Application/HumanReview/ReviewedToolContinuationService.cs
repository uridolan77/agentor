using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Application.HumanReview;

/// <summary>
/// After an approval decision, continues the originally blocked tool call and optional multi-step plan.
/// </summary>
public sealed class ReviewedToolContinuationService(
    IToolRegistry toolRegistry,
    ReviewPolicyReevaluationService policyReevaluation,
    IToolExecutionPipeline toolExecutionPipeline,
    IClock clock,
    ReviewTraceWriter traceWriter,
    PlanResumeOrchestrator planResumeOrchestrator,
    IAgentPlanExecutor planExecutor)
{
    public async Task ContinueApprovedToolExecutionAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var step = run.Steps.LastOrDefault(s => s.Status == AgentStepStatus.Running);
        if (step is null)
        {
            run.Fail("Run resumed from review but no active step was found.", clock.UtcNow);
            return;
        }

        var toolCall = step.ToolCalls.LastOrDefault(t => t.Status == ToolCallStatus.Running);
        if (toolCall is null)
        {
            run.Fail("Run resumed from review but no active tool call was found.", clock.UtcNow);
            return;
        }

        if (!toolRegistry.TryGetRegistration(toolCall.ToolKey, out var registration) || registration is null)
        {
            run.Fail($"Unknown tool '{toolCall.ToolKey}' after review resume.", clock.UtcNow);
            return;
        }

        var policyDecision = await policyReevaluation.EvaluateAfterHumanApprovalAsync(
            run,
            step.Id,
            toolCall.ToolKey,
            toolCall.InputPayload.ToPolicyEvaluationDictionary(),
            cancellationToken);

        step.AddPolicyDecision(policyDecision);
        traceWriter.RecordPostReviewPolicyEvaluated(run, step.Id, policyDecision);

        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            toolCall.Deny(policyDecision.Reason, clock.UtcNow);
            step.Fail(clock.UtcNow);
            run.Fail(policyDecision.Reason, clock.UtcNow);
            return;
        }

        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            toolCall.MarkRequiresReview(policyDecision.Reason, clock.UtcNow);
            step.MarkRequiresReview(clock.UtcNow);
            run.EnterRequiresReview(policyDecision.Reason, clock.UtcNow);
            return;
        }

        traceWriter.RecordPostReviewToolCallStarted(run, step.Id, toolCall.Id, toolCall.ToolKey);

        var pipelineResult = await toolExecutionPipeline.ExecuteAsync(
            run,
            step.Id,
            toolCall.Id,
            registration.Executor,
            new ToolExecutionRequest(run.Id, step.Id, toolCall.ToolKey, toolCall.InputPayload),
            cancellationToken);

        if (pipelineResult.Success)
        {
            toolCall.Succeed(pipelineResult.Output!, clock.UtcNow);
            traceWriter.RecordToolCallCompletedAfterReview(
                run,
                toolCall.Id,
                toolCall.Status,
                pipelineResult.AttemptsUsed,
                pipelineResult.TotalDuration);

            var cursor = run.ResumeCursor;
            if (cursor?.SkillContinuation is not null)
            {
                var skillResult = await planExecutor.ContinueSkillProcedureAfterInnerToolApprovalAsync(
                    run,
                    cursor,
                    pipelineResult.Output!,
                    cancellationToken);

                if (skillResult.SuspendedAgainForReview)
                {
                    return;
                }

                if (run.Status != AgentRunStatus.Running)
                {
                    return;
                }

                traceWriter.RecordStepCompletedAfterReview(run, step.Id);
                run.ClearResumeCursor(clock.UtcNow);

                var tailCursor = new PlanResumeCursor(
                    cursor.PlanId,
                    cursor.BlockedAtPlanStepId,
                    cursor.BlockedAtSourceStepId,
                    cursor.BlockedAtToolKey,
                    cursor.RemainingSteps,
                    cursor.CompletedStepHistory,
                    clock.UtcNow,
                    SkillContinuation: null);

                if (tailCursor.HasRemainingSteps)
                {
                    await planResumeOrchestrator.ResumeRemainingPlanStepsAsync(
                        run,
                        tailCursor,
                        skillResult.SkillPlanStepOutputForTailResume,
                        cancellationToken);
                    return;
                }

                traceWriter.RecordPlanExecutionCompletedAfterReview(run, cursor.PlanId);
                run.Complete(clock.UtcNow);
                return;
            }

            step.Complete(clock.UtcNow);
            traceWriter.RecordStepCompletedAfterReview(run, step.Id);

            if (cursor is { HasRemainingSteps: true })
            {
                run.ClearResumeCursor(clock.UtcNow);
                await planResumeOrchestrator.ResumeRemainingPlanStepsAsync(run, cursor, pipelineResult.Output, cancellationToken);
                return;
            }

            run.Complete(clock.UtcNow);
        }
        else
        {
            toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", clock.UtcNow);
            step.Fail(clock.UtcNow);
            run.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", clock.UtcNow);
        }
    }
}
