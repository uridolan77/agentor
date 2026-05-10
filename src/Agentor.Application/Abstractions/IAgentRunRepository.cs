using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public interface IAgentRunRepository
{
    Task SaveAsync(AgentRun run, CancellationToken cancellationToken);

    Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken);
}
