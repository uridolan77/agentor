using Agentor.Application;
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

    public SequentialAgentPlanExecutor(
        IToolRegistry registry,
        IPolicyEvaluator policy,
        IToolExecutionPipeline pipeline,
        IClock clock,
        IStepGuardEvaluator guards,
        ISkillPackageCatalog skills)
    {
        _registry = registry;
        _policy = policy;
        _pipeline = pipeline;
        _clock = clock;
        _guards = guards;
        _skills = skills;
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
                if (await ExecuteSkillPlanStepAsync(run, plan, ps, runStep, input, stepResults, ctx, state, cancellationToken).ConfigureAwait(false))
                {
                    plan.RefreshDerivedStatus();
                    return Finalize(plan, run, stepResults, state.PrimaryFailure, state.Escalation);
                }

                continue;
            }

            if (await ExecuteTopLevelToolPlanStepAsync(run, plan, ps, runStep, ps.ToolKey, input, stepResults, ctx, state, cancellationToken).ConfigureAwait(false))
            {
                plan.RefreshDerivedStatus();
                return Finalize(plan, run, stepResults, state.PrimaryFailure, state.Escalation);
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
        return Finalize(plan, run, stepResults, state.PrimaryFailure, state.Escalation);
    }

    /// <summary>Returns true when the executor must return immediately (terminal failure / review).</summary>
    private async Task<bool> ExecuteSkillPlanStepAsync(
        AgentRun run,
        AgentPlan plan,
        AgentPlanStep ps,
        AgentStep runStep,
        IReadOnlyDictionary<string, string> input,
        List<PlanStepResult> stepResults,
        PlanExecutionContext ctx,
        PlanExecutorLoopState state,
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
                TraceData(run, plan, ps));
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
                TraceData(run, plan, ps, "skillKey", ps.InvokedSkillKey, "skillVersion", ps.InvokedSkillVersion.Value));
            stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, PolicyDecisionOutcome.Deny, 0, new StepFailureSummary(fr, RetryDisposition.None), EscalationDisposition.None));
            return true;
        }

        run.RecordTrace(
            TraceEventKind.SkillInvocationStarted,
            $"Skill invocation started: {skill.SkillKey}@{skill.Version.Value}.",
            _clock.UtcNow,
            TraceData(run, plan, ps, "skillKey", skill.SkillKey, "skillVersion", skill.Version.Value));

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
                    TraceData(run, plan, ps, "procedureStepId", proc.StepId, "segmentName", proc.Name));
                continue;
            }

            if (proc.Kind != SkillProcedureStepKind.ToolRef || string.IsNullOrWhiteSpace(proc.ToolKey))
            {
                continue;
            }

            var inner = await ExecuteSkillInnerToolAsync(
                run,
                plan,
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
                run.RecordTrace(
                    TraceEventKind.SkillInvocationCompleted,
                    "Skill invocation ended (terminal state).",
                    _clock.UtcNow,
                    TraceData(run, plan, ps, "skillKey", skill.SkillKey));
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
                    TraceData(run, plan, ps, "skillKey", skill.SkillKey, "planStepStatus", ps.Status.ToString()));
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
        var snapshotOutput = SnapshotOutput(ps, lastToolOutput ?? StepInputBinding.Empty.Parameters);
        ctx.History.Add(new PlanStepExecutionSnapshot(ps.Id, ps.SourceStepId, ps.Status, true, snapshotOutput));
        run.RecordTrace(
            TraceEventKind.SkillInvocationCompleted,
            $"Skill invocation completed: {skill.SkillKey}@{skill.Version.Value}.",
            _clock.UtcNow,
            TraceData(run, plan, ps, "skillKey", skill.SkillKey));
        run.RecordTrace(
            TraceEventKind.PlanExecutionStepCompleted,
            $"Plan step completed: {ps.SourceStepId}.",
            _clock.UtcNow,
            TraceData(run, plan, ps, "attemptsUsed", totalAttempts.ToString()));
        stepResults.Add(new PlanStepResult(ps.Id, ps.SourceStepId, ps.Status, lastPolicy, totalAttempts, null, EscalationDisposition.None));

        return false;
    }

    private sealed record InnerToolResult(
        bool ReturnFromExecutor,
        bool AbortProcedure,
        int PipelineAttempts,
        PolicyDecisionOutcome? PolicyOutcome,
        StepFailureSummary? Failure,
        IReadOnlyDictionary<string, string>? Output);

    private async Task<InnerToolResult> ExecuteSkillInnerToolAsync(
        AgentRun run,
        AgentPlan plan,
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
                TraceData(run, plan, ps, "toolKey", toolKey, "procedureStepId", skillProcedureStepId));
            return new InnerToolResult(true, false, 0, PolicyDecisionOutcome.Deny, new StepFailureSummary(fr, RetryDisposition.None), null);
        }

        var policyDecision = await _policy.EvaluateToolCallAsync(
            new PolicyEvaluationRequest(run.Id, runStep.Id, toolKey, input),
            cancellationToken);

        runStep.AddPolicyDecision(policyDecision);
        run.RecordTrace(
            TraceEventKind.PolicyEvaluated,
            "Tool policy evaluated (skill inner).",
            _clock.UtcNow,
            TraceData(run, plan, ps, "outcome", policyDecision.Outcome.ToString(), "reasonCode", policyDecision.ReasonCode, "toolKey", toolKey, "procedureStepId", skillProcedureStepId));

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
            var skip = state.SkipRemaining;
            var esc = state.Escalation;
            var handled = ApplyFailurePolicyCore(run, plan, ps, runStep, new StepFailureSummary(fr, RetryDisposition.None), ref skip, ref esc);
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
                "Plan execution requires review (policy during skill).",
                _clock.UtcNow,
                TraceData(run, plan, ps, "procedureStepId", skillProcedureStepId, "toolKey", toolKey));
            return new InnerToolResult(true, false, 0, policyDecision.Outcome, null, null);
        }

        run.RecordTrace(
            TraceEventKind.ToolCallStarted,
            "Tool call started (skill inner).",
            _clock.UtcNow,
            TraceData(run, plan, ps, "toolCallId", toolCall.Id.ToString(), "toolKey", toolKey, "procedureStepId", skillProcedureStepId));

        var pipelineResult = await _pipeline.ExecuteAsync(
            run,
            runStep.Id,
            toolCall.Id,
            registration.Executor,
            new ToolExecutionRequest(run.Id, runStep.Id, toolKey, input),
            cancellationToken).ConfigureAwait(false);

        if (pipelineResult.Success)
        {
            toolCall.Succeed(pipelineResult.Output!, _clock.UtcNow);
            runStep.AddToolCall(toolCall);
            return new InnerToolResult(false, false, pipelineResult.AttemptsUsed, policyDecision.Outcome, null, pipelineResult.Output);
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
            TraceData(run, plan, ps, "onFailure", ps.OnFailure.ToString(), "retryDisposition", failure.RetryDisposition.ToString(), "procedureStepId", skillProcedureStepId));

        var skip2 = state.SkipRemaining;
        var esc2 = state.Escalation;
        var handledFailure = ApplyToolFailureOutcomeCore(run, plan, ps, runStep, failure, ref skip2, ref esc2);
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
            new PolicyEvaluationRequest(run.Id, runStep.Id, toolKey, input),
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
            new ToolExecutionRequest(run.Id, runStep.Id, toolKey, input),
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
        var r = ApplyFailurePolicyCore(run, plan, ps, runStep, failure, ref skip, ref esc);
        state.SkipRemaining = skip;
        state.Escalation = esc;
        return Task.FromResult(r);
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
        PlanExecutorLoopState state,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var skip = state.SkipRemaining;
        var esc = state.Escalation;
        var r = ApplyToolFailureOutcomeCore(run, plan, ps, runStep, failure, ref skip, ref esc);
        state.SkipRemaining = skip;
        state.Escalation = esc;
        return Task.FromResult(r);
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
        // Run completed without escalation; not the same as "every plan step succeeded" (see AgentPlanExecutionResult.Success).
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
