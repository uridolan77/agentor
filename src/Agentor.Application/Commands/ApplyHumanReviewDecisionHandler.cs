using Agentor.Application.Abstractions;
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
                new PolicyEvaluationContext(ResumeAfterApprovedHumanReview: true)),
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

            run.Complete(_clock.UtcNow);
        }
        else
        {
            toolCall.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
            step.Fail(_clock.UtcNow);
            run.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
        }
    }
}
