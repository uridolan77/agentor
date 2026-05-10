using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;

namespace Agentor.Application.Commands;

public sealed record ApplyHumanReviewDecisionCommand(
    Guid RunId,
    ReviewDecisionKind Kind,
    string? Note);

public sealed class ApplyHumanReviewDecisionHandler
{
    private readonly IAgentRunRepository _repository;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutionPipeline _toolExecutionPipeline;
    private readonly ICurrentActorAccessor _actorAccessor;
    private readonly IClock _clock;

    public ApplyHumanReviewDecisionHandler(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolRegistry toolRegistry,
        IToolExecutionPipeline toolExecutionPipeline,
        ICurrentActorAccessor actorAccessor,
        IClock clock)
    {
        _repository = repository;
        _policyEvaluator = policyEvaluator;
        _toolRegistry = toolRegistry;
        _toolExecutionPipeline = toolExecutionPipeline;
        _actorAccessor = actorAccessor;
        _clock = clock;
    }

    public async Task<AgentRun?> HandleAsync(ApplyHumanReviewDecisionCommand command, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(command.RunId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        if (run.Status != AgentRunStatus.RequiresReview)
        {
            throw new InvalidOperationException(
                $"Human review decisions apply only while the run requires review. Current status: {run.Status}.");
        }

        var actorId = _actorAccessor.Current.ActorId;
        if (actorId == Guid.Empty)
        {
            throw new InvalidOperationException("Actor id is required for human review decisions.");
        }

        var resolution = command.Kind switch
        {
            ReviewDecisionKind.Approve => ReviewResolutionStatus.ResolvedApproved,
            ReviewDecisionKind.Reject => ReviewResolutionStatus.ResolvedRejected,
            ReviewDecisionKind.RequestChanges => ReviewResolutionStatus.ChangesRequested,
            ReviewDecisionKind.Escalate => ReviewResolutionStatus.Escalated,
            _ => ReviewResolutionStatus.Pending
        };

        var decision = new HumanReviewDecision(
            Guid.NewGuid(),
            command.Kind,
            actorId,
            _clock.UtcNow,
            command.Note,
            resolution);

        run.ApplyHumanReviewDecision(decision, _clock.UtcNow);

        if (command.Kind == ReviewDecisionKind.Approve && run.Status == AgentRunStatus.Running)
        {
            await ContinueApprovedToolExecutionAsync(run, cancellationToken);
        }

        await _repository.SaveAsync(run, cancellationToken);
        return run;
    }

    private async Task ContinueApprovedToolExecutionAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var step = run.Steps.LastOrDefault(s => s.Status == AgentStepStatus.Running);
        if (step is null)
        {
            run.Fail("Run resumed from review but no active step was found.", _clock.UtcNow);
            return;
        }

        var toolCall = step.ToolCalls.LastOrDefault(t => t.Status == ToolCallStatus.Running);
        if (toolCall is null)
        {
            run.Fail("Run resumed from review but no active tool call was found.", _clock.UtcNow);
            return;
        }

        if (!_toolRegistry.TryGetRegistration(toolCall.ToolKey, out var registration) || registration is null)
        {
            run.Fail($"Unknown tool '{toolCall.ToolKey}' after review resume.", _clock.UtcNow);
            return;
        }

        var policyDecision = await _policyEvaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(
                run.Id,
                step.Id,
                toolCall.ToolKey,
                toolCall.Input,
                new PolicyEvaluationContext(ResumeAfterApprovedHumanReview: true),
                run.ToPolicyScope()),
            cancellationToken);

        step.AddPolicyDecision(policyDecision);
        run.RecordTrace(TraceEventKind.PolicyEvaluated, "Tool policy evaluated (post human review).", _clock.UtcNow, new Dictionary<string, string>
        {
            ["stepId"] = step.Id.ToString(),
            ["outcome"] = policyDecision.Outcome.ToString(),
            ["reasonCode"] = policyDecision.ReasonCode
        });

        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
            step.Fail(_clock.UtcNow);
            run.Fail(policyDecision.Reason, _clock.UtcNow);
            return;
        }

        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
            step.MarkRequiresReview(_clock.UtcNow);
            run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
            return;
        }

        run.RecordTrace(TraceEventKind.ToolCallStarted, "Tool call started (after human review).", _clock.UtcNow, new Dictionary<string, string>
        {
            ["stepId"] = step.Id.ToString(),
            ["toolCallId"] = toolCall.Id.ToString(),
            ["toolKey"] = toolCall.ToolKey
        });

        var pipelineResult = await _toolExecutionPipeline.ExecuteAsync(
            run,
            step.Id,
            toolCall.Id,
            registration.Executor,
            new ToolExecutionRequest(run.Id, step.Id, toolCall.ToolKey, toolCall.Input),
            cancellationToken);

        if (pipelineResult.Success)
        {
            toolCall.Succeed(pipelineResult.Output!, _clock.UtcNow);
            run.RecordTrace(TraceEventKind.ToolCallCompleted, "Tool call completed.", _clock.UtcNow, new Dictionary<string, string>
            {
                ["toolCallId"] = toolCall.Id.ToString(),
                ["status"] = toolCall.Status.ToString(),
                ["attemptsUsed"] = pipelineResult.AttemptsUsed.ToString(),
                ["totalDurationMs"] = ((long)pipelineResult.TotalDuration.TotalMilliseconds).ToString()
            });

            step.Complete(_clock.UtcNow);
            run.RecordTrace(TraceEventKind.StepCompleted, "Step completed.", _clock.UtcNow, new Dictionary<string, string>
            {
                ["stepId"] = step.Id.ToString()
            });

            // Multi-step resume: if the suspended plan had remaining steps, continue them now.
            var cursor = run.ResumeCursor;
            if (cursor is { HasRemainingSteps: true })
            {
                run.ClearResumeCursor(_clock.UtcNow);
                await ResumeRemainingPlanStepsAsync(run, cursor, pipelineResult.Output, cancellationToken);
                return;
            }

            run.Complete(_clock.UtcNow);
        }
        else
        {
            toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
            step.Fail(_clock.UtcNow);
            run.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
        }
    }

    /// <summary>
    /// Executes remaining plan steps from a cursor after the originally-blocked step has been approved and executed.
    /// Each step is fully policy-evaluated and run through the tool execution pipeline.
    /// The run is completed (or failed) on exit.
    /// </summary>
    private async Task ResumeRemainingPlanStepsAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        IReadOnlyDictionary<string, string>? approvedStepOutput,
        CancellationToken cancellationToken)
    {
        run.RecordTrace(
            TraceEventKind.MultiStepPlanResumed,
            $"Multi-step plan resuming {cursor.RemainingSteps.Count} remaining step(s) after approval of '{cursor.BlockedAtSourceStepId}'.",
            _clock.UtcNow,
            new Dictionary<string, string>
            {
                ["planId"] = cursor.PlanId.ToString("D"),
                ["blockedAtSourceStepId"] = cursor.BlockedAtSourceStepId,
                ["remainingSteps"] = cursor.RemainingSteps.Count.ToString()
            });

        // Rebuild execution context from cursor history + the just-approved step's output.
        var ctx = new PlanExecutionContext();
        foreach (var h in cursor.CompletedStepHistory)
        {
            ctx.History.Add(new PlanStepExecutionSnapshot(h.PlanStepId, h.SourceStepId, h.FinalStatus, h.ToolSucceeded, h.Output));
        }
        ctx.History.Add(new PlanStepExecutionSnapshot(
            cursor.BlockedAtPlanStepId,
            cursor.BlockedAtSourceStepId,
            AgentPlanStepStatus.Completed,
            approvedStepOutput is not null,
            approvedStepOutput));

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
            run.Complete(_clock.UtcNow);
            run.RecordTrace(
                TraceEventKind.PlanExecutionCompleted,
                "Multi-step plan execution completed after human review resume.",
                _clock.UtcNow,
                new Dictionary<string, string> { ["planId"] = cursor.PlanId.ToString("D") });
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
        // Skill step resume is not supported in Phase 18.
        if (pending.Kind == RecipeStepKind.Skill)
        {
            run.RecordTrace(
                TraceEventKind.PlanExecutionFailed,
                $"Resumed plan step '{pending.SourceStepId}' cannot execute: skill step resume is not supported in this runtime version.",
                _clock.UtcNow,
                new Dictionary<string, string>
                {
                    ["planId"] = cursor.PlanId.ToString("D"),
                    ["sourceStepId"] = pending.SourceStepId,
                    ["reasonCode"] = "SKILL_RESUME_NOT_SUPPORTED"
                });

            if (pending.OnFailure is FailureHandlingPolicy.ContinueOnFailure or FailureHandlingPolicy.MarkForCompensation)
            {
                ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Failed, false, null));
                return true;
            }

            if (pending.OnFailure == FailureHandlingPolicy.SkipRemaining)
            {
                ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Failed, false, null));
                return false;
            }

            run.Fail("Skill step resume is not supported.", _clock.UtcNow);
            return false;
        }

        var input = BuildResumedStepInput(run, pending);

        if (!_toolRegistry.TryGetRegistration(pending.ToolKey, out var registration) || registration is null)
        {
            run.Fail($"Unknown tool '{pending.ToolKey}' during plan resume.", _clock.UtcNow);
            return false;
        }

        var runStep = run.StartStep($"PlanResume:{cursor.PlanId:N}:{pending.SourceStepId}", _clock.UtcNow);

        // Evaluate fresh — approval of a prior step grants no forward-looking license to subsequent tool calls.
        var policyDecision = await _policyEvaluator.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(run.Id, runStep.Id, pending.ToolKey, input, Scope: run.ToPolicyScope()),
            cancellationToken);

        runStep.AddPolicyDecision(policyDecision);
        run.RecordTrace(
            TraceEventKind.PolicyEvaluated,
            $"Policy evaluated for resumed step '{pending.SourceStepId}'.",
            _clock.UtcNow,
            new Dictionary<string, string>
            {
                ["planId"] = cursor.PlanId.ToString("D"),
                ["sourceStepId"] = pending.SourceStepId,
                ["toolKey"] = pending.ToolKey,
                ["outcome"] = policyDecision.Outcome.ToString()
            });

        var toolCall = ToolCall.Start(run.Id, runStep.Id, pending.ToolKey, input, _clock.UtcNow);

        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            if (pending.OnFailure == FailureHandlingPolicy.EscalateToReview)
            {
                toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
                runStep.AddToolCall(toolCall);
                runStep.MarkRequiresReview(_clock.UtcNow);
                run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
                ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.RequiresReview, false, null));
                RecordNewCursorForResumedStep(run, cursor, pending, ctx);
                return false;
            }

            toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Failed, false, null));

            if (pending.OnFailure is FailureHandlingPolicy.ContinueOnFailure or FailureHandlingPolicy.MarkForCompensation)
            {
                runStep.Complete(_clock.UtcNow);
                return true;
            }

            if (pending.OnFailure == FailureHandlingPolicy.SkipRemaining)
            {
                runStep.Complete(_clock.UtcNow);
                return false;
            }

            runStep.Fail(_clock.UtcNow);
            run.Fail(policyDecision.Reason, _clock.UtcNow);
            return false;
        }

        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            runStep.MarkRequiresReview(_clock.UtcNow);
            run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.RequiresReview, false, null));
            RecordNewCursorForResumedStep(run, cursor, pending, ctx);
            return false;
        }

        run.RecordTrace(
            TraceEventKind.ToolCallStarted,
            $"Tool call started for resumed step '{pending.SourceStepId}'.",
            _clock.UtcNow,
            new Dictionary<string, string>
            {
                ["toolCallId"] = toolCall.Id.ToString(),
                ["toolKey"] = pending.ToolKey,
                ["sourceStepId"] = pending.SourceStepId
            });

        runStep.AddToolCall(toolCall);

        var pipelineResult = await _toolExecutionPipeline.ExecuteAsync(
            run,
            runStep.Id,
            toolCall.Id,
            registration.Executor,
            new ToolExecutionRequest(run.Id, runStep.Id, pending.ToolKey, input),
            cancellationToken);

        if (pipelineResult.Success)
        {
            toolCall.Succeed(pipelineResult.Output!, _clock.UtcNow);
            runStep.Complete(_clock.UtcNow);

            IReadOnlyDictionary<string, string>? snapshotOutput = pipelineResult.Output;
            if (pending.OutputBinding is not null && pipelineResult.Output!.TryGetValue(pending.OutputBinding.NormalizedKey, out var bound))
            {
                snapshotOutput = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [pending.OutputBinding.NormalizedKey] = bound };
            }

            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Completed, true, snapshotOutput));
            run.RecordTrace(
                TraceEventKind.PlanExecutionStepCompleted,
                $"Resumed plan step completed: {pending.SourceStepId}.",
                _clock.UtcNow,
                new Dictionary<string, string>
                {
                    ["planId"] = cursor.PlanId.ToString("D"),
                    ["sourceStepId"] = pending.SourceStepId,
                    ["attemptsUsed"] = pipelineResult.AttemptsUsed.ToString()
                });
            return true;
        }

        if (pending.OnFailure == FailureHandlingPolicy.EscalateToReview)
        {
            toolCall.MarkRequiresReview(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
            runStep.MarkRequiresReview(_clock.UtcNow);
            run.EnterRequiresReview(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
            ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.RequiresReview, false, null));
            RecordNewCursorForResumedStep(run, cursor, pending, ctx);
            return false;
        }

        toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
        ctx.History.Add(new PlanStepExecutionSnapshot(pending.PlanStepId, pending.SourceStepId, AgentPlanStepStatus.Failed, false, null));

        if (pending.OnFailure is FailureHandlingPolicy.ContinueOnFailure or FailureHandlingPolicy.MarkForCompensation)
        {
            runStep.Complete(_clock.UtcNow);
            return true;
        }

        if (pending.OnFailure == FailureHandlingPolicy.SkipRemaining)
        {
            runStep.Complete(_clock.UtcNow);
            return false;
        }

        runStep.Fail(_clock.UtcNow);
        run.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
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

        var newCursor = new PlanResumeCursor(
            originalCursor.PlanId,
            blockedStep.PlanStepId,
            blockedStep.SourceStepId,
            blockedStep.ToolKey,
            newRemaining,
            newHistory,
            _clock.UtcNow);

        run.RecordPlanResumeCursor(newCursor, _clock.UtcNow);
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
