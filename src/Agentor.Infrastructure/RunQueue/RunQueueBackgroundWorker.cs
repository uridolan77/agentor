using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Application.Observability;
using Agentor.Application.Orchestration;
using Agentor.Application.RunQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.RunQueue;

/// <summary>Drains durable queued runs when background worker execution is enabled.</summary>
public sealed class RunQueueHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<RunQueueOptions> _options;
    private readonly IOptionsMonitor<RunWorkerOptions> _workerOptions;
    private readonly ILogger<RunQueueHostedService> _logger;
    private readonly IRuntimeMetricsRecorder _metrics;
    private readonly string _workerId = $"run-worker:{Environment.MachineName}:{Guid.NewGuid():N}";

    public RunQueueHostedService(
        IServiceScopeFactory scopeFactory,
        IClock clock,
        IOptionsMonitor<RunQueueOptions> options,
        IOptionsMonitor<RunWorkerOptions> workerOptions,
        ILogger<RunQueueHostedService>? logger = null,
        IRuntimeMetricsRecorder? metrics = null)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options;
        _workerOptions = workerOptions;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<RunQueueHostedService>.Instance;
        _metrics = metrics ?? NullRuntimeMetricsRecorder.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await TryProcessSingleAsync(stoppingToken).ConfigureAwait(false);
            if (!processed)
            {
                await Task.Delay(_workerOptions.CurrentValue.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task<bool> TryProcessSingleAsync(CancellationToken cancellationToken)
    {
        if (_options.CurrentValue.ExecutionMode == RunQueueExecutionMode.Inline)
        {
            return false;
        }

        if (!_workerOptions.CurrentValue.Enabled)
        {
            return false;
        }

        await using var iterationScope = _scopeFactory.CreateAsyncScope();
        var scopedServices = iterationScope.ServiceProvider;
        var queueStore = scopedServices.GetRequiredService<IDurableRunQueue>();
        var leaseStore = scopedServices.GetRequiredService<IRunExecutionLeaseStore>();

        var now = _clock.UtcNow;
        var leaseTtl = _workerOptions.CurrentValue.LeaseTtl;

        var item = await queueStore
            .TryClaimNextAsync(_workerId, leaseTtl, now, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return false;
        }

        _metrics.RecordQueueClaimed();
        using (_logger.BeginScope(new[]
        {
            new KeyValuePair<string, object?>(AgentorLogFields.WorkItemId, item.WorkItemId),
        }))
        {
            _logger.LogInformation(AgentorEventIds.QueueClaimed, "queue.claimed");
        }

        var lease = await leaseStore
            .TryAcquireAsync(item.WorkItemId, _workerId, leaseTtl, now, cancellationToken)
            .ConfigureAwait(false);

        if (lease == LeaseAcquireOutcome.Contested)
        {
            await queueStore.ReleaseClaimAsync(item.WorkItemId, _workerId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return false;
        }

        try
        {
            await ProcessOneAsync(item, queueStore, scopedServices, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            await leaseStore.ReleaseAsync(item.WorkItemId, _workerId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(
        RunQueueRecord item,
        IDurableRunQueue queueStore,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        try
        {
            var orchestrator = scopedServices.GetRequiredService<IAgentRunOrchestrator>();
            var publicRuns = scopedServices.GetRequiredService<IOptionsMonitor<AgentorPublicRunOptions>>();
            if (!StartAgentRunRouting.TryBuildRequest(item.Command, publicRuns.CurrentValue, out var request, out var errors)
                || request is null)
            {
                throw new RunOrchestrationValidationException(errors ?? Array.Empty<string>());
            }

            var run = await orchestrator.StartAsync(request, cancellationToken).ConfigureAwait(false);
            await queueStore
                .MarkCompletedAsync(item.WorkItemId, run.Id, _workerId, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            _metrics.RecordQueueCompleted();
            _logger.LogInformation(AgentorEventIds.QueueCompleted, "queue.completed");
        }
        catch (Exception ex)
        {
            var safe = ObservabilityRedaction.SanitizeForLog(ex.Message);
            await queueStore
                .MarkFailedAsync(item.WorkItemId, safe, _workerId, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
            _metrics.RecordQueueFailed();
            _logger.LogWarning(AgentorEventIds.QueueFailed, "queue.failed {Detail}", safe);
        }
    }
}
