using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.RunQueue;

/// <summary>Drains the in-memory run queue when <see cref="RunQueueOptions.ExecutionMode"/> is <see cref="RunQueueExecutionMode.InMemoryBackground"/>.</summary>
public sealed class RunQueueBackgroundWorker : BackgroundService
{
    private readonly InMemoryRunQueue _queue;
    private readonly IOptionsMonitor<RunQueueOptions> _options;

    public RunQueueBackgroundWorker(InMemoryRunQueue queue, IOptionsMonitor<RunQueueOptions> options)
    {
        _queue = queue;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.CurrentValue.ExecutionMode != RunQueueExecutionMode.InMemoryBackground)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            RunWorkItem item;
            try
            {
                item = await _queue.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await _queue.ProcessOneAsync(item, stoppingToken).ConfigureAwait(false);
        }
    }
}
