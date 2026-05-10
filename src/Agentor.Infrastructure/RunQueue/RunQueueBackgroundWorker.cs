using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.RunQueue;

/// <summary>Drains durable queued runs when background worker execution is enabled.</summary>
public sealed class RunQueueHostedService : BackgroundService
{
    private readonly IDurableRunQueue _queueStore;
    private readonly IRunExecutionLeaseStore _leaseStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock;
    private readonly IOptionsMonitor<RunQueueOptions> _options;
    private readonly IOptionsMonitor<RunWorkerOptions> _workerOptions;
    private readonly string _workerId = $"run-worker:{Environment.MachineName}:{Guid.NewGuid():N}";

    public RunQueueHostedService(
        IDurableRunQueue queueStore,
        IRunExecutionLeaseStore leaseStore,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        IOptionsMonitor<RunQueueOptions> options,
        IOptionsMonitor<RunWorkerOptions> workerOptions)
    {
        _queueStore = queueStore;
        _leaseStore = leaseStore;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options;
        _workerOptions = workerOptions;
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

        var now = _clock.UtcNow;
        var leaseTtl = _workerOptions.CurrentValue.LeaseTtl;

        var item = await _queueStore
            .TryClaimNextAsync(_workerId, leaseTtl, now, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return false;
        }

        var lease = await _leaseStore
            .TryAcquireAsync(item.WorkItemId, _workerId, leaseTtl, now, cancellationToken)
            .ConfigureAwait(false);

        if (lease == LeaseAcquireOutcome.Contested)
        {
            await _queueStore.ReleaseClaimAsync(item.WorkItemId, _workerId, _clock.UtcNow, cancellationToken).ConfigureAwait(false);
            return false;
        }

        try
        {
            await ProcessOneAsync(item, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            await _leaseStore.ReleaseAsync(item.WorkItemId, _workerId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(RunQueueRecord item, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<StartAgentRunHandler>();
            var run = await handler.HandleAsync(item.Command, cancellationToken).ConfigureAwait(false);
            await _queueStore
                .MarkCompletedAsync(item.WorkItemId, run.Id, _workerId, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _queueStore
                .MarkFailedAsync(item.WorkItemId, ex.Message, _workerId, _clock.UtcNow, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
