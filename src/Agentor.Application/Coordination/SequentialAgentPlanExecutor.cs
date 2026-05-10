using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Coordination;

public sealed record PlanStepResult(
    Guid PlanStepId,
    string SourceStepId,
    AgentPlanStepStatus FinalStatus,
    PolicyDecisionOutcome? PolicyOutcome,
    int PipelineAttemptsUsed,
    StepFailureSummary? Failure,
    EscalationDisposition Escalation);

public sealed record AgentPlanExecutionResult(
    bool Success,
    AgentPlanStatus PlanStatus,
    AgentRunStatus RunStatus,
    IReadOnlyList<PlanStepResult> StepResults,
    PlanFailureSummary Summary);

public interface IAgentPlanExecutor
{
    Task<AgentPlanExecutionResult> ExecuteAsync(AgentRun run, AgentPlan plan, CancellationToken cancellationToken);
}

public sealed class SequentialAgentPlanExecutor : IAgentPlanExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IPolicyEvaluator _policy;
    private readonly IToolExecutionPipeline _pipeline;
    private readonly IClock _clock;
    private readonly IStepGuardEvaluator _guards;

    public SequentialAgentPlanExecutor(
        IToolRegistry registry,
        IPolicyEvaluator policy,
        IToolExecutionPipeline pipeline,
        IClock clock,
        IStepGuardEvaluator guards)
    {
        _registry = registry;
        _policy = policy;
        _pipeline = pipeline;
        _clock = clock;
        _guards = guards;
    }

    public async Task<AgentPlanExecutionResult> ExecuteAsync(AgentRun run, AgentPlan plan, CancellationToken cancellationToken)
    {
        var stepResults = new List<PlanStepResult>();
        var ctx = new PlanExecutionContext();
        FailureReason? primaryFailure = null;
        var escalation = EscalationDisposition.None;

        plan.Status = AgentPlanStatus.Running;
        run.RecordTrace(
            TraceEventKind.PlanExecutionStarted,
            "Sequential plan execution started.",
            _clock.UtcNow,
            TraceData(run, plan));

        var skipRemaining = false;

        foreach (var ps in plan.Steps.OrderBy(s => s.OrderIndex))
        {
            if (skipRemaining)
            {
                ps.Status = AgentPlanStepStatus.Skipped;
                ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
                run.RecordTrace(
                    TraceEventKind.PlanStepSkipped,
                    $"Plan step skipped (skip remaining): {ps.SourceStepId}.",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "skipRemaining", "true"));
                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, null, 0, null, EscalationDisposition.None));
                continue;
            }

            var guardResult = _guards.Evaluate(ps, ctx);
            run.RecordTrace(
                TraceEventKind.StepGuardEvaluated,
                "Step guard evaluated.",
                _clock.UtcNow,
                TraceData(run, plan, ps, "guardDecision", guardResult.Decision.ToString(), "guardReason", guardResult.ReasonCode));

            if (guardResult.Decision == GuardedStepDecision.Skip)
            {
                ps.Status = AgentPlanStepStatus.Skipped;
                ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
                run.RecordTrace(
                    TraceEventKind.PlanStepSkipped,
                    $"Plan step skipped by guard: {ps.SourceStepId}.",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "guardReason", guardResult.ReasonCode));
                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, null, 0, null, EscalationDisposition.None));
                continue;
            }

            AgentStateMachine.EnsurePlanStepExecutable(ps);

            run.RecordTrace(
                TraceEventKind.PlanExecutionStepStarted,
                $"Plan step started: {ps.SourceStepId}.",
                _clock.UtcNow,
                TraceData(run, plan, ps));

            var runStep = run.StartStep($"Plan:{plan.Id:N}:{ps.SourceStepId}", _clock.UtcNow);
            var input = BuildInput(run, ps);

            if (!_registry.TryGetRegistration(ps.ToolKey, out var registration) || registration is null)
            {
                var fr = new FailureReason("UNKNOWN_TOOL", $"Tool '{ps.ToolKey}' is not registered.", FailureCategory.Policy);
                primaryFailure ??= fr;
                ps.Status = AgentPlanStepStatus.Failed;
                runStep.Fail(_clock.UtcNow);
                run.Fail(fr.Message, _clock.UtcNow);
                run.RecordTrace(
                    TraceEventKind.PlanExecutionFailed,
                    "Plan execution failed (unknown tool).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps));
                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, PolicyDecisionOutcome.Deny, 0, new StepFailureSummary(fr, RetryDisposition.None), EscalationDisposition.None));
                plan.RefreshDerivedStatus();
                return Finalize(plan, run, stepResults, primaryFailure, escalation);
            }

            var policyDecision = await _policy.EvaluateToolCallAsync(
                new PolicyEvaluationRequest(run.Id, runStep.Id, ps.ToolKey, input),
                cancellationToken);

            runStep.AddPolicyDecision(policyDecision);
            run.RecordTrace(
                TraceEventKind.PolicyEvaluated,
                "Tool policy evaluated (plan step).",
                _clock.UtcNow,
                TraceData(run, plan, ps, "outcome", policyDecision.Outcome.ToString(), "reasonCode", policyDecision.ReasonCode));

            var toolCall = ToolCall.Start(run.Id, runStep.Id, ps.ToolKey, input, _clock.UtcNow);

            if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
            {
                toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
                runStep.AddToolCall(toolCall);
                ps.Status = AgentPlanStepStatus.Failed;
                var fr = new FailureReason(policyDecision.ReasonCode, policyDecision.Reason, FailureCategory.Policy);
                primaryFailure ??= fr;
                var handled = await ApplyFailurePolicyAsync(
                    run,
                    plan,
                    ps,
                    runStep,
                    toolCall,
                    new StepFailureSummary(fr, RetryDisposition.None),
                    ref skipRemaining,
                    ref escalation,
                    cancellationToken).ConfigureAwait(false);

                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, 0, new StepFailureSummary(fr, RetryDisposition.None), escalation));
                ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));

                if (!handled)
                {
                    plan.RefreshDerivedStatus();
                    return Finalize(plan, run, stepResults, primaryFailure, escalation);
                }

                run.RecordTrace(
                    TraceEventKind.PlanExecutionStepCompleted,
                    $"Plan step coordination completed: {ps.SourceStepId}.",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "finalStatus", ps.Status.ToString()));
                continue;
            }

            if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
            {
                toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
                runStep.AddToolCall(toolCall);
                runStep.MarkRequiresReview(_clock.UtcNow);
                ps.Status = AgentPlanStepStatus.RequiresReview;
                run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
                escalation = EscalationDisposition.EscalatedToReview;
                run.RecordTrace(
                    TraceEventKind.PlanExecutionRequiresReview,
                    "Plan execution requires review (policy).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps));
                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, 0, null, escalation));
                ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
                plan.RefreshDerivedStatus();
                return Finalize(plan, run, stepResults, primaryFailure, escalation);
            }

            run.RecordTrace(
                TraceEventKind.ToolCallStarted,
                "Tool call started (plan step).",
                _clock.UtcNow,
                TraceData(run, plan, ps, "toolCallId", toolCall.Id.ToString()));

            var pipelineResult = await _pipeline.ExecuteAsync(
                run,
                runStep.Id,
                toolCall.Id,
                registration.Executor,
                new ToolExecutionRequest(run.Id, runStep.Id, ps.ToolKey, input),
                cancellationToken).ConfigureAwait(false);

            if (pipelineResult.Success)
            {
                toolCall.Succeed(pipelineResult.Output!, _clock.UtcNow);
                runStep.AddToolCall(toolCall);
                runStep.Complete(_clock.UtcNow);
                ps.Status = AgentPlanStepStatus.Completed;
                var snapshotOutput = SnapshotOutput(ps, pipelineResult.Output!);
                ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, true, snapshotOutput));
                run.RecordTrace(
                    TraceEventKind.PlanExecutionStepCompleted,
                    $"Plan step completed: {ps.SourceStepId}.",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "attemptsUsed", pipelineResult.AttemptsUsed.ToString()));
                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, pipelineResult.AttemptsUsed, null, EscalationDisposition.None));
                continue;
            }

            toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
            runStep.AddToolCall(toolCall);

            var failure = new StepFailureSummary(
                new FailureReason("TOOL_PIPELINE", pipelineResult.ErrorMessage ?? "Tool execution failed.", FailureCategory.ToolExecution),
                ps.OnFailure == FailureHandlingPolicy.RetryViaToolPipelineOnly
                    ? RetryDisposition.DelegatedToToolPipeline
                    : RetryDisposition.None);

            primaryFailure ??= failure.Reason;

            run.RecordTrace(
                TraceEventKind.PlanFailureDecisionRecorded,
                "Plan step failure recorded.",
                _clock.UtcNow,
                TraceData(run, plan, ps, "onFailure", ps.OnFailure.ToString(), "retryDisposition", failure.RetryDisposition.ToString()));

            var handledFailure = await ApplyToolFailureOutcomeAsync(
                run,
                plan,
                ps,
                runStep,
                failure,
                ref skipRemaining,
                ref escalation,
                cancellationToken).ConfigureAwait(false);

            ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, pipelineResult.AttemptsUsed, failure, escalation));

            if (!handledFailure)
            {
                plan.RefreshDerivedStatus();
                return Finalize(plan, run, stepResults, primaryFailure, escalation);
            }

            run.RecordTrace(
                TraceEventKind.PlanExecutionStepCompleted,
                $"Plan step coordination completed after failure policy: {ps.SourceStepId}.",
                _clock.UtcNow,
                TraceData(run, plan, ps, "finalStatus", ps.Status.ToString()));
        }

        if (run.Status == AgentRunStatus.Running)
        {
            run.Complete(_clock.UtcNow);
            run.RecordTrace(
                TraceEventKind.PlanExecutionCompleted,
                "Sequential plan execution completed.",
                _clock.UtcNow,
                TraceData(run, plan));
        }

        plan.RefreshDerivedStatus();
        return Finalize(plan, run, stepResults, primaryFailure, escalation);
    }

    private Task<bool> ApplyFailurePolicyAsync(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        ToolCall toolCall,
        StepFailureSummary failure,
        ref bool skipRemaining,
        ref EscalationDisposition escalation,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(ApplyFailurePolicyCore(run, plan, ps, runStep, failure, ref skipRemaining, ref escalation));
    }

    private bool ApplyFailurePolicyCore(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        StepFailureSummary failure,
        ref bool skipRemaining,
        ref EscalationDisposition escalation)
    {
        switch (ps.OnFailure)
        {
            case FailureHandlingPolicy.ContinueOnFailure:
                runStep.Complete(_clock.UtcNow);
                return true;
            case FailureHandlingPolicy.MarkForCompensation:
                if (ps.CompensationHook is not null)
                {
                    ps.CompensationStatus = CompensationStatus.Pending;
                    run.RecordTrace(
                        TraceEventKind.CompensationHookRecorded,
                        "Compensation hook recorded (metadata only).",
                        _clock.UtcNow,
                        TraceData(run, plan, ps, "hookId", ps.CompensationHook.HookId));
                }

                runStep.Complete(_clock.UtcNow);
                return true;
            case FailureHandlingPolicy.SkipRemaining:
                runStep.Complete(_clock.UtcNow);
                skipRemaining = true;
                return true;
            case FailureHandlingPolicy.EscalateToReview:
                ps.Status = AgentPlanStepStatus.RequiresReview;
                runStep.MarkRequiresReview(_clock.UtcNow);
                run.EnterRequiresReview(failure.Reason.Message, _clock.UtcNow);
                escalation = EscalationDisposition.EscalatedToReview;
                return false;
            case FailureHandlingPolicy.RetryViaToolPipelineOnly:
            case FailureHandlingPolicy.FailFast:
            default:
                runStep.Fail(_clock.UtcNow);
                run.Fail(failure.Reason.Message, _clock.UtcNow);
                run.RecordTrace(TraceEventKind.PlanExecutionFailed, "Plan execution failed (policy deny).", _clock.UtcNow, TraceData(run, plan, ps));
                return false;
        }
    }

    private Task<bool> ApplyToolFailureOutcomeAsync(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        StepFailureSummary failure,
        ref bool skipRemaining,
        ref EscalationDisposition escalation,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(ApplyToolFailureOutcomeCore(run, plan, ps, runStep, failure, ref skipRemaining, ref escalation));
    }

    private bool ApplyToolFailureOutcomeCore(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        StepFailureSummary failure,
        ref bool skipRemaining,
        ref EscalationDisposition escalation)
    {
        switch (ps.OnFailure)
        {
            case FailureHandlingPolicy.ContinueOnFailure:
                ps.Status = AgentPlanStepStatus.Failed;
                runStep.Complete(_clock.UtcNow);
                return true;
            case FailureHandlingPolicy.MarkForCompensation:
                ps.Status = AgentPlanStepStatus.Failed;
                if (ps.CompensationHook is not null)
                {
                    ps.CompensationStatus = CompensationStatus.Recorded;
                    run.RecordTrace(
                        TraceEventKind.CompensationHookRecorded,
                        "Compensation hook recorded (metadata only).",
                        _clock.UtcNow,
                        TraceData(run, plan, ps, "hookId", ps.CompensationHook.HookId));
                }

                runStep.Complete(_clock.UtcNow);
                return true;
            case FailureHandlingPolicy.SkipRemaining:
                ps.Status = AgentPlanStepStatus.Failed;
                runStep.Complete(_clock.UtcNow);
                skipRemaining = true;
                return true;
            case FailureHandlingPolicy.EscalateToReview:
                ps.Status = AgentPlanStepStatus.RequiresReview;
                runStep.MarkRequiresReview(_clock.UtcNow);
                run.EnterRequiresReview(failure.Reason.Message, _clock.UtcNow);
                escalation = EscalationDisposition.EscalatedToReview;
                return false;
            case FailureHandlingPolicy.RetryViaToolPipelineOnly:
            case FailureHandlingPolicy.FailFast:
            default:
                ps.Status = AgentPlanStepStatus.Failed;
                runStep.Fail(_clock.UtcNow);
                run.Fail(failure.Reason.Message, _clock.UtcNow);
                run.RecordTrace(TraceEventKind.PlanExecutionFailed, "Plan execution failed (tool).", _clock.UtcNow, TraceData(run, plan, ps));
                return false;
        }
    }

    private static AgentPlanExecutionResult Finalize(
        AgentPlan plan,
        AgentRun run,
        List<PlanStepResult> stepResults,
        FailureReason? primaryFailure,
        EscalationDisposition escalation)
    {
        plan.RefreshDerivedStatus();
        var overall = run.Status == AgentRunStatus.Completed && escalation == EscalationDisposition.None;
        var summary = new PlanFailureSummary(
            run.Status is AgentRunStatus.Failed or AgentRunStatus.RequiresReview,
            primaryFailure,
            escalation);
        return new AgentPlanExecutionResult(overall, plan.Status, run.Status, stepResults, summary);
    }

    private static IReadOnlyDictionary<string, string>? SnapshotOutput(AgentPlanStep ps, IReadOnlyDictionary<string, string> output)
    {
        if (ps.OutputBinding is null)
        {
            return output;
        }

        var key = ps.OutputBinding.NormalizedKey;
        if (output.TryGetValue(key, out var single))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [key] = single };
        }

        return output;
    }

    private static Dictionary<string, string> BuildInput(AgentRun run, AgentPlanStep ps)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["objective"] = run.Objective,
            ["agentName"] = run.AgentName
        };

        if (ps.InputBinding is not null)
        {
            foreach (var kv in ps.InputBinding.Parameters)
            {
                d[kv.Key] = kv.Value;
            }
        }

        return d;
    }

    private static Dictionary<string, string> TraceData(AgentRun run, AgentPlan plan, AgentPlanStep? step = null, params string[] extraPairs)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runId"] = run.Id.ToString(),
            ["planId"] = plan.Id.ToString()
        };

        if (step is not null)
        {
            d["planStepId"] = step.Id.ToString();
            d["sourceStepId"] = step.SourceStepId;
        }

        for (var i = 0; i + 1 < extraPairs.Length; i += 2)
        {
            d[extraPairs[i]] = extraPairs[i + 1];
        }

        return d;
    }
}
