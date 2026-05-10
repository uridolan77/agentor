using Agentor.Application.Abstractions;
using Agentor.Application.Options;
using Agentor.Application.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Reliability;

public sealed class OutboxHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<OutboxDispatchOptions> _options;

    public OutboxHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<OutboxDispatchOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var dispatched = await DispatchOnceAsync(stoppingToken).ConfigureAwait(false);
            if (!dispatched)
            {
                await Task.Delay(_options.CurrentValue.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    internal async Task<bool> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            return false;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
        var sink = scope.ServiceProvider.GetRequiredService<IOutboxSink>();

        var dispatched = await dispatcher
            .DispatchPendingAsync(sink, _options.CurrentValue.SafeBatchSize, cancellationToken)
            .ConfigureAwait(false);
        return dispatched > 0;
    }
}
