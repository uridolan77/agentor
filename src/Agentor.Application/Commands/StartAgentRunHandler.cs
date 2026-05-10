using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Commands;

public sealed class StartAgentRunHandler
{
    private readonly IAgentRunRepository _repository;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolExecutionPipeline _toolExecutionPipeline;
    private readonly IClock _clock;

    public StartAgentRunHandler(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolRegistry toolRegistry,
        IToolExecutionPipeline toolExecutionPipeline,
        IClock clock)
    {
        _repository = repository;
        _policyEvaluator = policyEvaluator;
        _toolRegistry = toolRegistry;
        _toolExecutionPipeline = toolExecutionPipeline;
        _clock = clock;
    }

    public async Task<AgentRun> HandleAsync(StartAgentRunCommand command, CancellationToken cancellationToken)
    {
        var profile = AgentProfile.Create(
            string.IsNullOrWhiteSpace(command.AgentName) ? "PR1 Agent" : command.AgentName,
            "PR1 deterministic fake agent profile.",
            _clock.UtcNow);

        var traceId = string.IsNullOrWhiteSpace(command.TraceId)
            ? Guid.NewGuid().ToString("N")
            : command.TraceId.Trim();

        var scope = new AgentRunScope(
            command.TenantId,
            command.WorkspaceId,
            command.ProjectId,
            command.KnowledgeScopeId);

        var run = AgentRun.Start(profile.Id, profile.Name, command.Objective, traceId, _clock.UtcNow, scope);

        try
        {
            var step = run.StartStep("Execute deterministic fake tool", _clock.UtcNow);

            var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["objective"] = command.Objective,
                ["agentName"] = profile.Name
            };

            if (!_toolRegistry.TryGetRegistration(WellKnownToolKeys.Pr1FakeTool, out var registration)
                || registration is null)
            {
                run.RecordTrace(
                    TraceEventKind.PolicyEvaluated,
                    "Unknown tool; run cannot proceed.",
                    _clock.UtcNow,
                    new Dictionary<string, string>
                    {
                        ["stepId"] = step.Id.ToString(),
                        ["toolKey"] = WellKnownToolKeys.Pr1FakeTool
                    });
                step.Fail(_clock.UtcNow);
                run.Fail($"Unknown tool '{WellKnownToolKeys.Pr1FakeTool}'.", _clock.UtcNow);
                await _repository.SaveAsync(run, cancellationToken);
                return run;
            }

            var toolKey = registration.Definition.Key;

            var policyDecision = await _policyEvaluator.EvaluateToolCallAsync(
                new PolicyEvaluationRequest(run.Id, step.Id, toolKey, input, null),
                cancellationToken);

            step.AddPolicyDecision(policyDecision);
            run.RecordTrace(TraceEventKind.PolicyEvaluated, "Tool policy evaluated.", _clock.UtcNow, new Dictionary<string, string>
            {
                ["stepId"] = step.Id.ToString(),
                ["outcome"] = policyDecision.Outcome.ToString(),
                ["reasonCode"] = policyDecision.ReasonCode
            });

            var toolCall = ToolCall.Start(run.Id, step.Id, toolKey, input, _clock.UtcNow);

            if (policyDecision.Outcome == PolicyDecisionOutcome.Deny)
            {
                toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
                step.AddToolCall(toolCall);
                step.Fail(_clock.UtcNow);
                run.Fail(policyDecision.Reason, _clock.UtcNow);
                await _repository.SaveAsync(run, cancellationToken);
                return run;
            }

            if (policyDecision.Outcome == PolicyDecisionOutcome.RequiresReview)
            {
                toolCall.MarkRequiresReview(policyDecision.Reason, _clock.UtcNow);
                step.AddToolCall(toolCall);
                step.MarkRequiresReview(_clock.UtcNow);
                run.EnterRequiresReview(policyDecision.Reason, _clock.UtcNow);
                await _repository.SaveAsync(run, cancellationToken);
                return run;
            }

            run.RecordTrace(TraceEventKind.ToolCallStarted, "Tool call started.", _clock.UtcNow, new Dictionary<string, string>
            {
                ["stepId"] = step.Id.ToString(),
                ["toolCallId"] = toolCall.Id.ToString(),
                ["toolKey"] = toolKey
            });

            var pipelineResult = await _toolExecutionPipeline.ExecuteAsync(
                run,
                step.Id,
                toolCall.Id,
                registration.Executor,
                new ToolExecutionRequest(run.Id, step.Id, toolKey, input),
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

            await _repository.SaveAsync(run, cancellationToken);
            return run;
        }
        catch (Exception ex)
        {
            if (run.Status == AgentRunStatus.Running)
            {
                run.Fail(ex.Message, _clock.UtcNow);
            }

            await _repository.SaveAsync(run, cancellationToken);
            throw;
        }
    }
}