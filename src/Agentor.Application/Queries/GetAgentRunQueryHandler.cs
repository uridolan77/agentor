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

public sealed class ListAgentRunsQueryHandler
{
    public const int DefaultTake = 20;
    public const int MaxTake = 100;

    private readonly IAgentRunRepository _repository;

    public ListAgentRunsQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public Task<AgentRunListPage> HandleAsync(int skip, int take, CancellationToken cancellationToken)
    {
        if (skip < 0)
        {
            skip = 0;
        }

        if (take < 1)
        {
            take = DefaultTake;
        }

        if (take > MaxTake)
        {
            take = MaxTake;
        }

        return _repository.ListSummariesAsync(skip, take, cancellationToken, null);
    }
}

public sealed class GetAgentRunTraceQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public GetAgentRunTraceQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ExecutionTraceEvent>?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        return run is null ? null : run.Trace;
    }
}

public sealed class GetAgentRunStepsQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public GetAgentRunStepsQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<AgentStep>?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        return run is null ? null : run.Steps;
    }
}

public sealed class GetAgentRunToolCallsQueryHandler
{
    private readonly IAgentRunRepository _repository;

    public GetAgentRunToolCallsQueryHandler(IAgentRunRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ToolCall>?> HandleAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _repository.GetAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        return run.Steps
            .OrderBy(s => s.Index)
            .SelectMany(s => s.ToolCalls)
            .ToList();
    }
}
