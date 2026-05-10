using Agentor.Application.Abstractions;
using Agentor.Domain;

namespace Agentor.Application.Queries;

public sealed class GetAgentRunQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public GetAgentRunQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public Task<AgentRun?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        return _repository.GetAsync(runId, cancellationToken);
    }
}
