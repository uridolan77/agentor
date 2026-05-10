using Agentor.Application.Options;
using Agentor.Application.Orchestration;
using Agentor.Domain;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Commands;

public sealed class StartAgentRunHandler
{
    private readonly IAgentRunOrchestrator _orchestrator;
    private readonly IOptionsMonitor<AgentorPublicRunOptions> _publicRunOptions;

    public StartAgentRunHandler(
        IAgentRunOrchestrator orchestrator,
        IOptionsMonitor<AgentorPublicRunOptions> publicRunOptions)
    {
        _orchestrator = orchestrator;
        _publicRunOptions = publicRunOptions;
    }

    public async Task<AgentRun> HandleAsync(StartAgentRunCommand command, CancellationToken cancellationToken)
    {
        if (!StartAgentRunRouting.TryBuildRequest(command, _publicRunOptions.CurrentValue, out var request, out var errors)
            || request is null)
        {
            throw new RunOrchestrationValidationException(errors ?? Array.Empty<string>());
        }

        return await _orchestrator.StartAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
