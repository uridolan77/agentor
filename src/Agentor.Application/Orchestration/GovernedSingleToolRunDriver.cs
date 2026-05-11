using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Observability;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agentor.Application.Orchestration;

/// <summary>
/// Executes one governed tool call on a new run (policy + pipeline), persisting terminal state.
/// </summary>
public sealed class GovernedSingleToolRunDriver
{
    private readonly IAgentRunRepository _repository;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutionPipeline _toolExecutionPipeline;
    private readonly IClock _clock;
    private readonly ILogger<GovernedSingleToolRunDriver> _logger;
    private readonly IRuntimeMetricsRecorder _metrics;

    public GovernedSingleToolRunDriver(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolRegistry toolRegistry,
        IToolExecutionPipeline toolExecutionPipeline,
        IClock clock,
        ILogger<GovernedSingleToolRunDriver>? logger = null,
        IRuntimeMetricsRecorder? metrics = null)
    {
        _repository = repository;
        _policyEvaluator = policyEvaluator;
        _toolRegistry = toolRegistry;
        _toolExecutionPipeline = toolExecutionPipeline;
        _clock = clock;
        _logger = logger ?? NullLogger<GovernedSingleToolRunDriver>.Instance;
        _metrics = metrics ?? NullRuntimeMetricsRecorder.Instance;
    }

    public async Task<AgentRun> ExecuteAsync(
        RunOrchestrationRequest request,
        string profilePurpose,
        string stepSummary,
        string toolKey,
        CancellationToken cancellationToken)
    {
        var profile = AgentProfile.Create(
            string.IsNullOrWhiteSpace(request.AgentName) ? "PR1 Agent" : request.AgentName.Trim(),
            profilePurpose,
            _clock.UtcNow);

        var traceId = string.IsNullOrWhiteSpace(request.TraceId)
            ? Guid.NewGuid().ToString("N")
            : request.TraceId.Trim();

        var scope = new AgentRunScope(
            request.TenantId,
            request.WorkspaceId,
            request.ProjectId,
            request.KnowledgeScopeId);

        var run = AgentRun.Start(profile.Id, profile.Name, request.Objective, traceId, _clock.UtcNow, scope);
        var terminalEmitted = false;

        void EmitRunTerminalOnce()
        {
            if (terminalEmitted)
            {
                return;
            }

            if (run.Status is AgentRunStatus.Running or AgentRunStatus.Queued)
            {
                return;
            }

            terminalEmitted = true;
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

        using (_logger.BeginScope(SafeLogContext.ForRun(run.Id, run.TraceId, AgentorCorrelationContext.Current)))
        {
            _metrics.RecordRunStarted();
            _logger.LogInformation(AgentorEventIds.RunStarted, "run.started");

            try
            {
                var step = run.StartStep(stepSummary, _clock.UtcNow);

                var basePayload = request.ToolInputPayload ?? ToolPayload.FromLegacyDictionary(request.ToolInput);

                var mergedSummary = new Dictionary<string, string>(basePayload.ToLegacySummary(), StringComparer.OrdinalIgnoreCase)
                {
                    ["objective"] = request.Objective,
                    ["agentName"] = profile.Name,
                };

                var mergedPayload = new ToolPayload(
                    basePayload.Body,
                    basePayload.SchemaId,
                    basePayload.ContentType,
                    mergedSummary);

                var policyInput = mergedPayload.ToPolicyEvaluationDictionary();

                if (!_toolRegistry.TryGetRegistration(toolKey, out var registration) || registration is null)
                {
                    run.RecordTrace(
                        TraceEventKind.PolicyEvaluated,
                        "Unknown tool; run cannot proceed.",
                        _clock.UtcNow,
                        new Dictionary<string, string>
                        {
                            ["stepId"] = step.Id.ToString(),
                            ["toolKey"] = toolKey
                        });
                    step.Fail(_clock.UtcNow);
                    run.Fail($"Unknown tool '{toolKey}'.", _clock.UtcNow);
                    await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
                    EmitRunTerminalOnce();
                    return run;
                }

                var resolvedKey = registration.Definition.Key;

                var policyDecision = await _policyEvaluator.EvaluateToolCallAsync(
                    new PolicyEvaluationRequest(run.Id, step.Id, resolvedKey, policyInput, null, run.ToPolicyScope()),
                    cancellationToken).ConfigureAwait(false);

                step.AddPolicyDecision(policyDecision);
                run.RecordTrace(TraceEventKind.PolicyEvaluated, "Tool policy evaluated.", _clock.UtcNow, new Dictionary<string, string>
                {
                    ["stepId"] = step.Id.ToString(),
                    ["outcome"] = policyDecision.Outcome.ToString(),
                    ["reasonCode"] = policyDecision.ReasonCode
                });

                var toolCall = ToolCall.Start(run.Id, step.Id, resolvedKey, mergedPayload, _clock.UtcNow);

                if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
                {
                    toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
                    step.AddToolCall(toolCall);
                    step.Fail(_clock.UtcNow);
                    run.Fail(policyDecision.Reason, _clock.UtcNow);
                    await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
                    EmitRunTerminalOnce();
                    return run;
                }

                if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
                {
                    toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
                    step.AddToolCall(toolCall);
                    step.MarkRequiresReview(_clock.UtcNow);
                    run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
                    await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
                    EmitRunTerminalOnce();
                    return run;
                }

                run.RecordTrace(TraceEventKind.ToolCallStarted, "Tool call started.", _clock.UtcNow, new Dictionary<string, string>
                {
                    ["stepId"] = step.Id.ToString(),
                    ["toolCallId"] = toolCall.Id.ToString(),
                    ["toolKey"] = resolvedKey
                });

                var pipelineResult = await _toolExecutionPipeline.ExecuteAsync(
                    run,
                    step.Id,
                    toolCall.Id,
                    registration.Executor,
                    new ToolExecutionRequest(run.Id, step.Id, resolvedKey, mergedPayload),
                    cancellationToken).ConfigureAwait(false);

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

                    step.AddToolCall(toolCall);
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
                    step.AddToolCall(toolCall);
                    step.Fail(_clock.UtcNow);
                    run.Fail(pipelineResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
                }

                await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
                EmitRunTerminalOnce();
                return run;
            }
            catch (Exception ex)
            {
                if (run.Status == AgentRunStatus.Running)
                {
                    run.Fail(ex.Message, _clock.UtcNow);
                }

                await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
                EmitRunTerminalOnce();
                throw;
            }
        }
    }
}
