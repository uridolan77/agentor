using Agentor.Application.Abstractions;
using Agentor.Application.Manifest;
using Agentor.Domain;

namespace Agentor.Application.Queries;

public sealed class GetRunManifestQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public GetRunManifestQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<RunManifest?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        return run is null ? null : RunManifest.FromRun(run, ModelCallTelemetryAggregator.Aggregate(run));
    }
}
