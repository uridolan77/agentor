using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Domain;

namespace Agentor.Application.Commands;

public sealed record ExecuteAgentPlanCommand(AgentRun Run, AgentPlan Plan);

public sealed class ExecuteAgentPlanHandler
{
    private readonly IAgentPlanExecutor _executor;
    private readonly IAgentRunRepository _repository;

    public ExecuteAgentPlanHandler(IAgentPlanExecutor executor, IAgentRunRepository repository)
    {
        _executor = executor;
        _repository = repository;
    }

    public async Task<AgentPlanExecutionResult> HandleAsync(ExecuteAgentPlanCommand command, CancellationToken cancellationToken)
    {
        var result = await _executor.ExecuteAsync(command.Run, command.Plan, cancellationToken).ConfigureAwait(false);
        await _repository.SaveAsync(command.Run, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
