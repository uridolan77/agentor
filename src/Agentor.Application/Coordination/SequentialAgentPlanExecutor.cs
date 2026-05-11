using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Observability;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Microsoft.Extensions.Logging;

namespace Agentor.Application.Coordination;

public sealed record SkillAfterApprovalContinuationResult(
    bool SuspendedAgainForReview,
    ToolPayload? SkillPlanStepOutputForTailResume);

public sealed record PlanStepResult(
    Guid PlanStepId,
    string SourceStepId,
    AgentPlanStepStatus FinalStatus,
    PolicyDecisionOutcome? PolicyOutcome,
    int PipelineAttemptsUsed,
    StepFailureSummary? Failure,
    EscalationDisposition Escalation);

/// <summary>
/// Outcome of coordinating a sequential <see cref="AgentPlan"/> against an <see cref="AgentRun"/>.
/// </summary>
/// <param name="Success">
/// True when the run reached <see cref="AgentRunStatus.Completed"/> without plan-level escalation
/// (<see cref="EscalationDisposition.None"/> in the coordinator). This does not imply every plan step
/// succeeded: with <see cref="FailureHandlingPolicy.ContinueOnFailure"/>, steps may appear as failed in
/// <see cref="StepResults"/> while the run still completes. Use <see cref="PlanStatus"/>, per-step results,
/// and <see cref="PlanFailureSummary"/> for plan health.
/// </param>
public sealed record AgentPlanExecutionResult(
    bool Success,
    AgentPlanStatus PlanStatus,
    AgentRunStatus RunStatus,
    IReadOnlyList<PlanStepResult> StepResults,
    PlanFailureSummary Summary);

public interface IAgentPlanExecutor
{
    Task<AgentPlanExecutionResult> ExecuteAsync(AgentRun run, AgentPlan plan, CancellationToken cancellationToken);

    Task<SkillAfterApprovalContinuationResult> ContinueSkillProcedureAfterInnerToolApprovalAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        ToolPayload approvedBlockedInnerOutput,
        CancellationToken cancellationToken);

    Task<bool> ExecuteResumedSkillPlanStepAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        PendingPlanStep pending,
        PlanExecutionContext ctx,
        CancellationToken cancellationToken);
}

internal sealed class PlanExecutorLoopState
{
    public bool SkipRemaining;

    public EscalationDisposition Escalation;

    public FailureReason? PrimaryFailure;
}

// TODO(PR20.5): SequentialAgentPlanExecutor mixes sequential flow, guards, policy, pipeline, failure
// policies, tracing, and finalization. Split into focused private/static helpers when a no-behavior-change refactor is scheduled.
public sealed class SequentialAgentPlanExecutor : IAgentPlanExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IPolicyEvaluator _policy;
    private readonly IToolExecutionPipeline _pipeline;
    private readonly IClock _clock;
    private readonly IStepGuardEvaluator _guards;
    private readonly ISkillPackageCatalog _skills;
    private readonly ILogger<SequentialAgentPlanExecutor> _logger;
    private readonly IRuntimeMetricsRecorder _metrics;

    public SequentialAgentPlanExecutor(
        IToolRegistry registry,
        IPolicyEvaluator policy,
        IToolExecutionPipeline pipeline,
        IClock clock,
        IStepGuardEvaluator guards,
        ISkillPackageCatalog skills,
        ILogger<SequentialAgentPlanExecutor>? logger = null,
        IRuntimeMetricsRecorder? metrics = null)
    {
        _registry = registry;
        _policy = policy;
        _pipeline = pipeline;
        _clock = clock;
        _guards = guards;
        _skills = skills;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SequentialAgentPlanExecutor>.Instance;
        _metrics = metrics ?? NullRuntimeMetricsRecorder.Instance;
    }

    public async Task<AgentPlanExecutionResult> ExecuteAsync(AgentRun run, AgentPlan plan, CancellationToken cancellationToken)
    {
        var stepResults = new List<PlanStepResult>();
        var ctx = new PlanExecutionContext();
        var state = new PlanExecutorLoopState();

        plan.Status = AgentPlanStatus.Running;
        run.RecordTrace(
            TraceEventKind.PlanExecutionStarted,
            "Sequential plan execution started.",
            _clock.UtcNow,
            TraceData(run, plan));

        _metrics.RecordRunStarted();
        using (_logger.BeginScope(SafeLogContext.ForRun(run.Id, run.TraceId, AgentorCorrelationContext.Current)))
        {
            _logger.LogInformation(AgentorEventIds.RunStarted, "run.started");
        }

        foreach (var ps in plan.Steps.OrderBy(s => s.OrderIndex))
        {
            if (state.SkipRemaining)
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
            var input = PlanInputBuilder.BuildToolStepInput(run, ps);

            if (ps.Kind == RecipeStepKind.Skill)
            {
                if (await ExecuteSkillPlanStepAsync(
                        run,
                        plan.Id,
                        plan,
                        ps,
                        runStep,
                        input,
                        stepResults,
                        ctx,
                        state,
                        innerReviewRecorder: null,
                        cancellationToken).ConfigureAwait(false))
                {
                    plan.RefreshDerivedStatus();
                    return CompletePlan(plan, run, stepResults, state.PrimaryFailure, state.Escalation);
                }

                continue;
            }

            if (await ExecuteTopLevelToolPlanStepAsync(run, plan, ps, runStep, ps.ToolKey, input, stepResults, ctx, state, cancellationToken).ConfigureAwait(false))
            {
                plan.RefreshDerivedStatus();
                return CompletePlan(plan, run, stepResults, state.PrimaryFailure, state.Escalation);
            }
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
        return CompletePlan(plan, run, stepResults, state.PrimaryFailure, state.Escalation);
    }

    private AgentPlanExecutionResult CompletePlan(
        AgentPlan plan,
        AgentRun run,
        List<PlanStepResult> stepResults,
        FailureReason? primaryFailure,
        EscalationDisposition escalation)
    {
        var result = Finalize(plan, run, stepResults, primaryFailure, escalation);
        RecordPlanRunTerminal(run);
        return result;
    }

    private void RecordPlanRunTerminal(AgentRun run)
    {
        using (_logger.BeginScope(SafeLogContext.ForRun(run.Id, run.TraceId, AgentorCorrelationContext.Current)))
        {
            switch (run.Status)
            {
                case AgentRunStatus.Completed:
                    _metrics.RecordRunCompleted();
                    _logger.LogInformation(AgentorEventIds.RunCompleted, "run.completed");
                    break;
                case AgentRunStatus.Failed:
                    _metrics.RecordRunFailed();
                    _logger.LogWarning(AgentorEventIds.RunFailed, "run.failed");
                    break;
                case AgentRunStatus.RequiresReview:
                    _metrics.RecordRunRequiresReview();
                    _logger.LogInformation(AgentorEventIds.RunRequiresReview, "run.requires_review");
                    break;
            }
        }
    }

    /// <summary>Returns true when the executor must return immediately (terminal failure / review).</summary>
    private async Task<bool> ExecuteSkillPlanStepAsync(
        AgentRun run,
        Guid planTraceId,
        AgentPlan? planForRemainingSteps,
        AgentPlanStep ps,
        AgentStep runStep,
        IReadOnlyDictionary<string, string> input,
        List<PlanStepResult> stepResults,
        PlanExecutionContext ctx,
        PlanExecutorLoopState state,
        Action<SkillProcedureStepDefinition, string, IReadOnlyDictionary<string, string>?>? innerReviewRecorder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ps.InvokedSkillKey) || ps.InvokedSkillVersion is null)
        {
            var fr = new FailureReason("SKILL_METADATA", "Skill plan step is missing invoked skill metadata.", FailureCategory.Policy);
            state.PrimaryFailure ??= fr;
            ps.Status = AgentPlanStepStatus.Failed;
            runStep.Fail(_clock.UtcNow);
            run.Fail(fr.Message, _clock.UtcNow);
            run.RecordTrace(
                TraceEventKind.PlanExecutionFailed,
                "Plan execution failed (invalid skill metadata).",
                _clock.UtcNow,
                TraceData(run, planTraceId, ps));
            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, PolicyDecisionOutcome.Deny, 0, new StepFailureSummary(fr, RetryDisposition.None), EscalationDisposition.None));
            return true;
        }

        if (!_skills.TryGet(ps.InvokedSkillKey, ps.InvokedSkillVersion, out var skill) || skill is null)
        {
            var fr = new FailureReason(
                "UNKNOWN_SKILL",
                $"Skill '{ps.InvokedSkillKey}' version '{ps.InvokedSkillVersion.Value}' is not registered.",
                FailureCategory.Policy);
            state.PrimaryFailure ??= fr;
            ps.Status = AgentPlanStepStatus.Failed;
            runStep.Fail(_clock.UtcNow);
            run.Fail(fr.Message, _clock.UtcNow);
            run.RecordTrace(
                TraceEventKind.PlanExecutionFailed,
                "Plan execution failed (unknown skill).",
                _clock.UtcNow,
                TraceData(run, planTraceId, ps, "skillKey", ps.InvokedSkillKey, "skillVersion", ps.InvokedSkillVersion.Value));
            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, PolicyDecisionOutcome.Deny, 0, new StepFailureSummary(fr, RetryDisposition.None), EscalationDisposition.None));
            return true;
        }

        run.RecordTrace(
            TraceEventKind.SkillInvocationStarted,
            $"Skill invocation started: {skill.SkillKey}@{skill.Version.Value}.",
            _clock.UtcNow,
            TraceData(run, planTraceId, ps, "skillKey", skill.SkillKey, "skillVersion", skill.Version.Value));

        IReadOnlyDictionary<string, string>? lastToolOutput = null;
        var totalAttempts = 0;
        PolicyDecisionOutcome? lastPolicy = null;

        foreach (var proc in skill.ProcedureSteps.OrderBy(s => s.OrderIndex))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (proc.Kind == SkillProcedureStepKind.Segment)
            {
                run.RecordTrace(
                    TraceEventKind.SkillProcedureSegmentRecorded,
                    $"Skill segment: {proc.Name}.",
                    _clock.UtcNow,
                    TraceData(run, planTraceId, ps, "procedureStepId", proc.StepId, "segmentName", proc.Name));
                continue;
            }

            if (proc.Kind != SkillProcedureStepKind.ToolRef || string.IsNullOrWhiteSpace(proc.ToolKey))
            {
                continue;
            }

            var inner = await ExecuteSkillInnerToolAsync(
                run,
                planTraceId,
                ps,
                runStep,
                proc.ToolKey.Trim(),
                proc.StepId,
                input,
                state,
                cancellationToken).ConfigureAwait(false);

            totalAttempts += inner.PipelineAttempts;
            lastPolicy = inner.PolicyOutcome;

            if (inner.ReturnFromExecutor)
            {
                if (inner.BlockedToolKeyForCursor is not null)
                {
                    if (innerReviewRecorder is not null)
                    {
                        innerReviewRecorder(proc, inner.BlockedToolKeyForCursor, lastToolOutput);
                    }
                    else
                    {
                        if (planForRemainingSteps is null)
                        {
                            throw new InvalidOperationException("Plan is required to record a skill inner-tool resume cursor.");
                        }

                        RecordSkillInnerResumeCursor(run, planForRemainingSteps, ps, proc, inner.BlockedToolKeyForCursor, lastToolOutput, ctx);
                    }
                }

                run.RecordTrace(
                    TraceEventKind.SkillInvocationCompleted,
                    "Skill invocation ended (terminal state).",
                    _clock.UtcNow,
                    TraceData(run, planTraceId, ps, "skillKey", skill.SkillKey));
                stepResults.Add(
                    new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, lastPolicy, totalAttempts, inner.Failure, state.Escalation));
                return true;
            }

            if (inner.AbortProcedure)
            {
                run.RecordTrace(
                    TraceEventKind.SkillInvocationCompleted,
                    "Skill invocation ended (procedure aborted).",
                    _clock.UtcNow,
                    TraceData(run, planTraceId, ps, "skillKey", skill.SkillKey, "planStepStatus", ps.Status.ToString()));
                ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
                stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, lastPolicy, totalAttempts, inner.Failure, state.Escalation));
                return false;
            }

            if (inner.Output is not null)
            {
                lastToolOutput = inner.Output;
            }
        }

        runStep.Complete(_clock.UtcNow);
        ps.Status = AgentPlanStepStatus.Completed;
        var snapshotOutput = SnapshotOutput(
            ps,
            ToolPayload.FromLegacyDictionary(lastToolOutput ?? StepInputBinding.Empty.Parameters));
        ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, true, snapshotOutput));
        run.RecordTrace(
            TraceEventKind.SkillInvocationCompleted,
            $"Skill invocation completed: {skill.SkillKey}@{skill.Version.Value}.",
            _clock.UtcNow,
            TraceData(run, planTraceId, ps, "skillKey", skill.SkillKey));
        run.RecordTrace(
            TraceEventKind.PlanExecutionStepCompleted,
            $"Plan step completed: {ps.SourceStepId}.",
            _clock.UtcNow,
            TraceData(run, planTraceId, ps, "attemptsUsed", totalAttempts.ToString()));
        stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, lastPolicy, totalAttempts, null, EscalationDisposition.None));

        return false;
    }

    private sealed record InnerToolResult(
        bool ReturnFromExecutor,
        bool AbortProcedure,
        int PipelineAttempts,
        PolicyDecisionOutcome? PolicyOutcome,
        StepFailureSummary? Failure,
        IReadOnlyDictionary<string, string>? Output,
        string? BlockedToolKeyForCursor = null);

    private async Task<InnerToolResult> ExecuteSkillInnerToolAsync(
        AgentRun run,
        Guid planId,
        AgentPlanStep ps,
        AgentStep runStep,
        string toolKey,
        string skillProcedureStepId,
        IReadOnlyDictionary<string, string> input,
        PlanExecutorLoopState state,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGetRegistration(toolKey, out var registration) || registration is null)
        {
            var fr = new FailureReason("UNKNOWN_TOOL", $"Tool '{toolKey}' is not registered.", FailureCategory.Policy);
            state.PrimaryFailure ??= fr;
            ps.Status = AgentPlanStepStatus.Failed;
            runStep.Fail(_clock.UtcNow);
            run.Fail(fr.Message, _clock.UtcNow);
            run.RecordTrace(
                TraceEventKind.PlanExecutionFailed,
                "Plan execution failed (unknown tool during skill).",
                _clock.UtcNow,
                TraceData(run, planId, ps, "toolKey", toolKey, "procedureStepId", skillProcedureStepId));
            return new InnerToolResult(true, false, 0, PolicyDecisionOutcome.Deny, new StepFailureSummary(fr, RetryDisposition.None), null);
        }

        var policyDecision = await _policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(run.Id, runStep.Id, toolKey, input, null, run.ToPolicyScope()),
            cancellationToken);

        runStep.AddPolicyDecision(policyDecision);
        run.RecordTrace(
            TraceEventKind.PolicyEvaluated,
            "Tool policy evaluated (skill inner).",
            _clock.UtcNow,
            TraceData(run, planId, ps, "outcome", policyDecision.Outcome.ToString(), "reasonCode", policyDecision.ReasonCode, "toolKey", toolKey, "procedureStepId", skillProcedureStepId));

        var toolCall = ToolCall.Start(run.Id, runStep.Id, toolKey, input, _clock.UtcNow);

        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
            {
                run.RecordTrace(
                    TraceEventKind.ExternalAgentInvocationDenied,
                    "External-agent tool denied by policy (not executed).",
                    _clock.UtcNow,
                    TraceData(run, planId, ps, "toolKey", toolKey, "reasonCode", policyDecision.ReasonCode));
            }
            toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            ps.Status = AgentPlanStepStatus.Failed;
            var fr = new FailureReason(policyDecision.ReasonCode, policyDecision.Reason, FailureCategory.Policy);
            state.PrimaryFailure ??= fr;
            var skip = state.SkipRemaining;
            var esc = state.Escalation;
            var handled = ApplyFailurePolicyCore(run, planId, ps, runStep, new StepFailureSummary(fr, RetryDisposition.None), ref skip, ref esc);
            state.SkipRemaining = skip;
            state.Escalation = esc;

            if (!handled)
            {
                return new InnerToolResult(true, false, 0, policyDecision.Outcome, new StepFailureSummary(fr, RetryDisposition.None), null);
            }

            return new InnerToolResult(false, true, 0, policyDecision.Outcome, new StepFailureSummary(fr, RetryDisposition.None), null);
        }

        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
            {
                run.RecordTrace(
                    TraceEventKind.ExternalAgentInvocationRequiresReview,
                    "External-agent tool requires review (not executed).",
                    _clock.UtcNow,
                    TraceData(run, planId, ps, "toolKey", toolKey, "reasonCode", policyDecision.ReasonCode));
            }
            toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            runStep.MarkRequiresReview(_clock.UtcNow);
            ps.Status = AgentPlanStepStatus.RequiresReview;
            run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
            state.Escalation = EscalationDisposition.EscalatedToReview;
            run.RecordTrace(
                TraceEventKind.PlanExecutionRequiresReview,
                "Plan execution requires review (policy during skill).",
                _clock.UtcNow,
                TraceData(run, planId, ps, "procedureStepId", skillProcedureStepId, "toolKey", toolKey));
            return new InnerToolResult(true, false, 0, policyDecision.Outcome, null, null, BlockedToolKeyForCursor: toolKey);
        }

        run.RecordTrace(
            TraceEventKind.ToolCallStarted,
            "Tool call started (skill inner).",
            _clock.UtcNow,
            TraceData(run, planId, ps, "toolCallId", toolCall.Id.ToString(), "toolKey", toolKey, "procedureStepId", skillProcedureStepId));

        var pipelineResult = await _pipeline.ExecuteAsync(
            run,
            runStep.Id,
            toolCall.Id,
            registration.Executor,
            new ToolExecutionRequest(run.Id, runStep.Id, toolKey, ToolPayload.FromLegacyDictionary(input)),
            cancellationToken).ConfigureAwait(false);

        if (pipelineResult.Success)
        {
            toolCall.Succeed(pipelineResult.Output!, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            return new InnerToolResult(false, false, pipelineResult.AttemptsUsed, policyDecision.Outcome, null, pipelineResult.Output?.ToPolicyEvaluationDictionary());
        }

        toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
        runStep.AddToolCall(toolCall);

        var failure = new StepFailureSummary(
            new FailureReason("TOOL_PIPELINE", pipelineResult.ErrorMessage ?? "Tool execution failed.", FailureCategory.ToolExecution),
            ps.OnFailure == FailureHandlingPolicy.RetryViaToolPipelineOnly
                ? RetryDisposition.DelegatedToToolPipeline
                : RetryDisposition.None);

        state.PrimaryFailure ??= failure.Reason;

        run.RecordTrace(
            TraceEventKind.PlanFailureDecisionRecorded,
            "Skill inner tool failure recorded.",
            _clock.UtcNow,
                TraceData(run, planId, ps, "onFailure", ps.OnFailure.ToString(), "retryDisposition", failure.RetryDisposition.ToString(), "procedureStepId", skillProcedureStepId));

        var skip2 = state.SkipRemaining;
        var esc2 = state.Escalation;
        var handledFailure = ApplyToolFailureOutcomeCore(run, planId, ps, runStep, failure, ref skip2, ref esc2);
        state.SkipRemaining = skip2;
        state.Escalation = esc2;

        if (!handledFailure)
        {
            return new InnerToolResult(true, false, pipelineResult.AttemptsUsed, policyDecision.Outcome, failure, null);
        }

        return new InnerToolResult(false, true, pipelineResult.AttemptsUsed, policyDecision.Outcome, failure, null);
    }

    /// <summary>Returns true when the executor must return immediately (terminal failure / review).</summary>
    private async Task<bool> ExecuteTopLevelToolPlanStepAsync(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        string toolKey,
        IReadOnlyDictionary<string, string> input,
        List<PlanStepResult> stepResults,
        PlanExecutionContext ctx,
        PlanExecutorLoopState state,
        CancellationToken cancellationToken)
    {
        if (!_registry.TryGetRegistration(toolKey, out var registration) || registration is null)
        {
            var fr = new FailureReason("UNKNOWN_TOOL", $"Tool '{toolKey}' is not registered.", FailureCategory.Policy);
            state.PrimaryFailure ??= fr;
            ps.Status = AgentPlanStepStatus.Failed;
            runStep.Fail(_clock.UtcNow);
            run.Fail(fr.Message, _clock.UtcNow);
            run.RecordTrace(
                TraceEventKind.PlanExecutionFailed,
                "Plan execution failed (unknown tool).",
                _clock.UtcNow,
                TraceData(run, plan, ps));
            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, PolicyDecisionOutcome.Deny, 0, new StepFailureSummary(fr, RetryDisposition.None), EscalationDisposition.None));
            return true;
        }

        var policyDecision = await _policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(run.Id, runStep.Id, toolKey, input, null, run.ToPolicyScope()),
            cancellationToken);

        runStep.AddPolicyDecision(policyDecision);
        run.RecordTrace(
            TraceEventKind.PolicyEvaluated,
            "Tool policy evaluated (plan step).",
            _clock.UtcNow,
            TraceData(run, plan, ps, "outcome", policyDecision.Outcome.ToString(), "reasonCode", policyDecision.ReasonCode));

        var toolCall = ToolCall.Start(run.Id, runStep.Id, toolKey, input, _clock.UtcNow);

        if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
        {
            if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
            {
                run.RecordTrace(
                    TraceEventKind.ExternalAgentInvocationDenied,
                    "External-agent tool denied by policy (not executed).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "toolKey", toolKey, "reasonCode", policyDecision.ReasonCode));
            }
            toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            ps.Status = AgentPlanStepStatus.Failed;
            var fr = new FailureReason(policyDecision.ReasonCode, policyDecision.Reason, FailureCategory.Policy);
            state.PrimaryFailure ??= fr;
            var handled = await ApplyFailurePolicyAsync(
                run,
                plan,
                ps,
                runStep,
                toolCall,
                new StepFailureSummary(fr, RetryDisposition.None),
                state,
                cancellationToken).ConfigureAwait(false);

            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, 0, new StepFailureSummary(fr, RetryDisposition.None), state.Escalation));
            ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));

            if (!handled)
            {
                return true;
            }

            run.RecordTrace(
                TraceEventKind.PlanExecutionStepCompleted,
                $"Plan step coordination completed: {ps.SourceStepId}.",
                _clock.UtcNow,
                TraceData(run, plan, ps, "finalStatus", ps.Status.ToString()));
            return false;
        }

        if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
        {
            if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
            {
                run.RecordTrace(
                    TraceEventKind.ExternalAgentInvocationRequiresReview,
                    "External-agent tool requires review (not executed).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "toolKey", toolKey, "reasonCode", policyDecision.ReasonCode));
            }
            toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            runStep.MarkRequiresReview(_clock.UtcNow);
            ps.Status = AgentPlanStepStatus.RequiresReview;
            run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
            state.Escalation = EscalationDisposition.EscalatedToReview;
            run.RecordTrace(
                TraceEventKind.PlanExecutionRequiresReview,
                "Plan execution requires review (policy).",
                _clock.UtcNow,
                TraceData(run, plan, ps));
            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, 0, null, state.Escalation));
            ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
            RecordResumeCursorIfNeeded(run, plan, ps, toolKey, ctx);
            return true;
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
            new ToolExecutionRequest(run.Id, runStep.Id, toolKey, ToolPayload.FromLegacyDictionary(input)),
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
            return false;
        }

        toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
        runStep.AddToolCall(toolCall);

        var failure = new StepFailureSummary(
            new FailureReason("TOOL_PIPELINE", pipelineResult.ErrorMessage ?? "Tool execution failed.", FailureCategory.ToolExecution),
            ps.OnFailure == FailureHandlingPolicy.RetryViaToolPipelineOnly
                ? RetryDisposition.DelegatedToToolPipeline
                : RetryDisposition.None);

        state.PrimaryFailure ??= failure.Reason;

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
            state,
            cancellationToken).ConfigureAwait(false);

        ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, false, null));
        stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, policyDecision.Outcome, pipelineResult.AttemptsUsed, failure, state.Escalation));

        if (!handledFailure)
        {
            return true;
        }

        run.RecordTrace(
            TraceEventKind.PlanExecutionStepCompleted,
            $"Plan step coordination completed after failure policy: {ps.SourceStepId}.",
            _clock.UtcNow,
            TraceData(run, plan, ps, "finalStatus", ps.Status.ToString()));
        return false;
    }

    private Task<bool> ApplyFailurePolicyAsync(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        ToolCall toolCall,
        StepFailureSummary failure,
        PlanExecutorLoopState state,
        CancellationToken cancellationToken)
    {
        _ = toolCall;
        _ = cancellationToken;
        var skip = state.SkipRemaining;
        var esc = state.Escalation;
        var r = ApplyFailurePolicyCore(run, plan.Id, ps, runStep, failure, ref skip, ref esc);
        state.SkipRemaining = skip;
        state.Escalation = esc;
        return Task.FromResult(r);
    }

    private bool ApplyFailurePolicyCore(
        AgentRun run,
        Guid planId,
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
                        TraceData(run, planId, ps, "hookId", ps.CompensationHook.HookId));
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
                run.RecordTrace(TraceEventKind.PlanExecutionFailed, "Plan execution failed (policy deny).", _clock.UtcNow, TraceData(run, planId, ps));
                return false;
        }
    }

    private Task<bool> ApplyToolFailureOutcomeAsync(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        StepFailureSummary failure,
        PlanExecutorLoopState state,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var skip = state.SkipRemaining;
        var esc = state.Escalation;
        var r = ApplyToolFailureOutcomeCore(run, plan.Id, ps, runStep, failure, ref skip, ref esc);
        state.SkipRemaining = skip;
        state.Escalation = esc;
        return Task.FromResult(r);
    }

    private bool ApplyToolFailureOutcomeCore(
        AgentRun run,
        Guid planId,
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
                        TraceData(run, planId, ps, "hookId", ps.CompensationHook.HookId));
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
                run.RecordTrace(TraceEventKind.PlanExecutionFailed, "Plan execution failed (tool).", _clock.UtcNow, TraceData(run, planId, ps));
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
        // Run completed without escalation; not the same as "every plan step succeeded" (see AgentPlanExecutionResult.Success).
        var overall = run.Status == AgentRunStatus.Completed && escalation == EscalationDisposition.None;
        var summary = new PlanFailureSummary(
            run.Status is AgentRunStatus.Failed or AgentRunStatus.RequiresReview,
            primaryFailure,
            escalation);
        return new AgentPlanExecutionResult(overall, plan.Status, run.Status, stepResults, summary);
    }

    private static IReadOnlyDictionary<string, string>? MergeSkillOutputs(
        IReadOnlyDictionary<string, string>? prior,
        IReadOnlyDictionary<string, string>? next)
    {
        if (prior is null || prior.Count == 0)
        {
            return next;
        }

        if (next is null || next.Count == 0)
        {
            return prior;
        }

        var d = new Dictionary<string, string>(prior, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in next)
        {
            d[kv.Key] = kv.Value;
        }

        return d;
    }

    private static IReadOnlyDictionary<string, string> BuildResumedPlanInput(AgentRun run, PendingPlanStep pending)
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

    public async Task<bool> ExecuteResumedSkillPlanStepAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        PendingPlanStep pending,
        PlanExecutionContext ctx,
        CancellationToken cancellationToken)
    {
        var ps = PendingPlanStepFactory.ToAgentPlanStep(pending);
        var runStep = run.StartStep($"PlanResume:{cursor.PlanId:N}:{pending.SourceStepId}", _clock.UtcNow);
        var input = BuildResumedPlanInput(run, pending);
        var stepResults = new List<PlanStepResult>();
        var state = new PlanExecutorLoopState();

        var remainingTail = cursor.RemainingSteps.Where(s => s.OrderIndex > pending.OrderIndex).ToList();
        var historySnapshots = ctx.History
            .Where(h => h.ToolSucceeded)
            .Select(h => new PlanStepResumeSnapshot(h.PlanStepId, h.SourceStepId, h.Status, h.ToolSucceeded, h.ToolOutput))
            .ToList();

        return await ExecuteSkillPlanStepAsync(
            run,
            cursor.PlanId,
            planForRemainingSteps: null,
            ps,
            runStep,
            input,
            stepResults,
            ctx,
            state,
            innerReviewRecorder: (proc, blockedKey, lastOut) =>
                RecordSkillInnerResumeState(run, cursor.PlanId, ps, remainingTail, historySnapshots, proc, blockedKey, lastOut),
            cancellationToken);
    }

    public async Task<SkillAfterApprovalContinuationResult> ContinueSkillProcedureAfterInnerToolApprovalAsync(
        AgentRun run,
        PlanResumeCursor cursor,
        ToolPayload approvedBlockedInnerOutput,
        CancellationToken cancellationToken)
    {
        var sc = cursor.SkillContinuation
            ?? throw new InvalidOperationException("Expected skill continuation on resume cursor.");

        if (string.IsNullOrWhiteSpace(sc.SkillPlanStep.InvokedSkillKey) || sc.SkillPlanStep.InvokedSkillVersion is null)
        {
            run.Fail("Skill continuation is missing invoked skill metadata.", _clock.UtcNow);
            return new SkillAfterApprovalContinuationResult(false, null);
        }

        if (!_skills.TryGet(sc.SkillPlanStep.InvokedSkillKey, sc.SkillPlanStep.InvokedSkillVersion, out var skill) || skill is null)
        {
            run.Fail($"Skill '{sc.SkillPlanStep.InvokedSkillKey}' is not registered.", _clock.UtcNow);
            return new SkillAfterApprovalContinuationResult(false, null);
        }

        var ps = PendingPlanStepFactory.ToAgentPlanStep(sc.SkillPlanStep);
        var runStep = run.Steps.LastOrDefault(s => s.Status == AgentStepStatus.Running);
        if (runStep is null)
        {
            run.Fail("No active step for skill continuation.", _clock.UtcNow);
            return new SkillAfterApprovalContinuationResult(false, null);
        }

        var input = PlanInputBuilder.BuildToolStepInput(run, ps);
        var state = new PlanExecutorLoopState();
        IReadOnlyDictionary<string, string>? rolling = MergeSkillOutputs(
            sc.State.LastInnerToolOutput,
            approvedBlockedInnerOutput.ToPolicyEvaluationDictionary());
        PolicyDecisionOutcome? lastPolicy = null;
        var totalAttempts = 0;

        foreach (var proc in skill.ProcedureSteps.OrderBy(s => s.OrderIndex))
        {
            if (proc.OrderIndex <= sc.BlockedAtInnerTool.ProcedureOrderIndex)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (proc.Kind == SkillProcedureStepKind.Segment)
            {
                run.RecordTrace(
                    TraceEventKind.SkillProcedureSegmentRecorded,
                    $"Skill segment (after inner approval): {proc.Name}.",
                    _clock.UtcNow,
                    TraceData(run, cursor.PlanId, ps, "procedureStepId", proc.StepId, "segmentName", proc.Name));
                continue;
            }

            if (proc.Kind != SkillProcedureStepKind.ToolRef || string.IsNullOrWhiteSpace(proc.ToolKey))
            {
                continue;
            }

            var inner = await ExecuteSkillInnerToolAsync(
                run,
                cursor.PlanId,
                ps,
                runStep,
                proc.ToolKey.Trim(),
                proc.StepId,
                input,
                state,
                cancellationToken).ConfigureAwait(false);

            totalAttempts += inner.PipelineAttempts;
            lastPolicy = inner.PolicyOutcome;

            if (inner.ReturnFromExecutor)
            {
                if (inner.BlockedToolKeyForCursor is not null)
                {
                    RecordSkillInnerResumeState(
                        run,
                        cursor.PlanId,
                        ps,
                        cursor.RemainingSteps,
                        cursor.CompletedStepHistory,
                        proc,
                        inner.BlockedToolKeyForCursor,
                        rolling);
                }

                return new SkillAfterApprovalContinuationResult(true, null);
            }

            if (inner.AbortProcedure)
            {
                return new SkillAfterApprovalContinuationResult(false, null);
            }

            if (inner.Output is not null)
            {
                rolling = inner.Output;
            }
        }

        runStep.Complete(_clock.UtcNow);
        ps.Status = AgentPlanStepStatus.Completed;
        var skillPayload = ToolPayload.FromLegacyDictionary(rolling ?? StepInputBinding.Empty.Parameters);
        var snapshotDict = SnapshotOutput(ps, skillPayload);
        run.RecordTrace(
            TraceEventKind.SkillInvocationCompleted,
            $"Skill invocation completed after inner approval: {skill.SkillKey}@{skill.Version.Value}.",
            _clock.UtcNow,
            TraceData(run, cursor.PlanId, ps, "skillKey", skill.SkillKey));

        var tailPayload = snapshotDict is not null
            ? ToolPayload.FromLegacyDictionary(snapshotDict)
            : skillPayload;

        return new SkillAfterApprovalContinuationResult(false, tailPayload);
    }

    private static IReadOnlyDictionary<string, string>? SnapshotOutput(AgentPlanStep ps, ToolPayload output)
    {
        var flat = output.ToPolicyEvaluationDictionary();

        if (ps.OutputBinding is null)
        {
            return flat;
        }

        var key = ps.OutputBinding.NormalizedKey;
        if (flat.TryGetValue(key, out var single))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [key] = single };
        }

        return flat;
    }

    /// <summary>
    /// Records a <see cref="PlanResumeCursor"/> on the run when execution suspends for review mid-plan.
    /// Only records when there are steps after the blocked step (multi-step resume is needed).
    /// </summary>
    private void RecordResumeCursorIfNeeded(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep blockedStep,
        string blockedToolKey,
        PlanExecutionContext ctx)
    {
        var remaining = plan.Steps
            .Where(s => s.OrderIndex > blockedStep.OrderIndex)
            .OrderBy(s => s.OrderIndex)
            .Select(s => new PendingPlanStep(
                s.Id,
                s.SourceStepId,
                s.OrderIndex,
                s.ToolKey,
                s.Kind,
                s.OnFailure,
                s.InputBinding?.Parameters,
                s.OutputBinding,
                s.InvokedSkillKey,
                s.InvokedSkillVersion))
            .ToList();

        if (remaining.Count == 0)
        {
            return;
        }

        // Only include steps that completed successfully — RequiresReview/Failed steps are not part of the resume context.
        var history = ctx.History
            .Where(h => h.ToolSucceeded)
            .Select(h => new PlanStepResumeSnapshot(h.PlanStepId, h.SourceStepId, h.Status, h.ToolSucceeded, h.ToolOutput))
            .ToList();

        var cursor = new PlanResumeCursor(
            plan.Id,
            blockedStep.Id,
            blockedStep.SourceStepId,
            blockedToolKey,
            remaining,
            history,
            _clock.UtcNow);

        run.RecordPlanResumeCursor(cursor, _clock.UtcNow);
    }

    private void RecordSkillInnerResumeCursor(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep blockedSkillStep,
        SkillProcedureStepDefinition proc,
        string blockedInnerToolKey,
        IReadOnlyDictionary<string, string>? lastToolOutputBeforeBlocked,
        PlanExecutionContext ctx)
    {
        var remaining = plan.Steps
            .Where(s => s.OrderIndex > blockedSkillStep.OrderIndex)
            .OrderBy(s => s.OrderIndex)
            .Select(PendingPlanStepFactory.FromAgentPlanStep)
            .ToList();

        var history = ctx.History
            .Where(h => h.ToolSucceeded)
            .Select(h => new PlanStepResumeSnapshot(h.PlanStepId, h.SourceStepId, h.Status, h.ToolSucceeded, h.ToolOutput))
            .ToList();

        RecordSkillInnerResumeState(
            run,
            plan.Id,
            blockedSkillStep,
            remaining,
            history,
            proc,
            blockedInnerToolKey,
            lastToolOutputBeforeBlocked);
    }

    private void RecordSkillInnerResumeState(
        AgentRun run,
        Guid planId,
        AgentPlanStep blockedSkillStep,
        IReadOnlyList<PendingPlanStep> remainingAfterBlockedPlanStep,
        IReadOnlyList<PlanStepResumeSnapshot> completedPlanStepHistory,
        SkillProcedureStepDefinition proc,
        string blockedInnerToolKey,
        IReadOnlyDictionary<string, string>? lastToolOutputBeforeBlocked)
    {
        var skillContinuation = new SkillResumeCursor(
            PendingPlanStepFactory.FromAgentPlanStep(blockedSkillStep),
            new SkillInnerToolCheckpoint(proc.StepId, blockedInnerToolKey.Trim(), proc.OrderIndex),
            new SkillProcedureResumeState(lastToolOutputBeforeBlocked));

        var cursor = new PlanResumeCursor(
            planId,
            blockedSkillStep.Id,
            blockedSkillStep.SourceStepId,
            blockedInnerToolKey,
            remainingAfterBlockedPlanStep,
            completedPlanStepHistory,
            _clock.UtcNow,
            skillContinuation);

        run.RecordPlanResumeCursor(cursor, _clock.UtcNow);
    }

    private static Dictionary<string, string> TraceData(AgentRun run, AgentPlan plan, AgentPlanStep? step = null, params string[] extraPairs) =>
        TraceData(run, plan.Id, step, extraPairs);

    private static Dictionary<string, string> TraceData(AgentRun run, AgentPlan plan) =>
        TraceData(run, plan.Id, null);

    private static Dictionary<string, string> TraceData(AgentRun run, Guid planId, AgentPlanStep? step = null, params string[] extraPairs)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runId"] = run.Id.ToString(),
            ["planId"] = planId.ToString()
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
