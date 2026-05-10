using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Commands;

public sealed class StartAgentRunHandler
{
    private const string FakeToolKey = "pr1.fake-tool";

    private readonly IAgentRunRepository _repository;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IToolExecutor _toolExecutor;
    private readonly IClock _clock;

    public StartAgentRunHandler(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolExecutor toolExecutor,
        IClock clock)
    {
        _repository = repository;
        _policyEvaluator = policyEvaluator;
        _toolExecutor = toolExecutor;
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

        var run = AgentRun.Start(profile.Id, profile.Name, command.Objective, traceId, _clock.UtcNow);

        try
        {
            var step = run.StartStep("Execute deterministic fake tool", _clock.UtcNow);

            var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["objective"] = command.Objective,
                ["agentName"] = profile.Name
            };

            var policyDecision = await _policyEvaluator.EvaluateToolCallAsync(
                new PolicyEvaluationRequest(run.Id, step.Id, FakeToolKey, input),
                cancellationToken);

            step.AddPolicyDecision(policyDecision);
            run.RecordTrace(TraceEventKind.PolicyEvaluated, "Tool policy evaluated.", _clock.UtcNow, new Dictionary<string, string>
            {
                ["stepId"] = step.Id.ToString(),
                ["outcome"] = policyDecision.Outcome.ToString(),
                ["reasonCode"] = policyDecision.ReasonCode
            });

            var toolCall = ToolCall.Start(run.Id, step.Id, FakeToolKey, input, _clock.UtcNow);

            if (!policyDecision.AllowsExecution)
            {
                toolCall.Deny(policyDecision.Reason, _clock.UtcNow);
                step.AddToolCall(toolCall);
                step.Fail(_clock.UtcNow);
                run.Fail(policyDecision.Reason, _clock.UtcNow);
                await _repository.SaveAsync(run, cancellationToken);
                return run;
            }

            run.RecordTrace(TraceEventKind.ToolCallStarted, "Fake tool call started.", _clock.UtcNow, new Dictionary<string, string>
            {
                ["stepId"] = step.Id.ToString(),
                ["toolKey"] = FakeToolKey
            });

            var toolResult = await _toolExecutor.ExecuteAsync(
                new ToolExecutionRequest(run.Id, step.Id, FakeToolKey, input),
                cancellationToken);

            if (toolResult.Success)
            {
                toolCall.Succeed(toolResult.Output, _clock.UtcNow);
                run.RecordTrace(TraceEventKind.ToolCallCompleted, "Fake tool call completed.", _clock.UtcNow, new Dictionary<string, string>
                {
                    ["toolCallId"] = toolCall.Id.ToString(),
                    ["status"] = toolCall.Status.ToString()
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
                toolCall.Fail(toolResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
                step.AddToolCall(toolCall);
                step.Fail(_clock.UtcNow);
                run.Fail(toolResult.ErrorMessage ?? "Tool execution failed.", _clock.UtcNow);
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
