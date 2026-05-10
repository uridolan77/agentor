using Agentor.Domain;

namespace Agentor.Application.Orchestration;

public interface IAgentRunOrchestrator
{
    Task<AgentRun> StartAsync(RunOrchestrationRequest request, CancellationToken cancellationToken);
}
