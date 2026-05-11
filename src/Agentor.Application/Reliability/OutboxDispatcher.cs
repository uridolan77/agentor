using Agentor.Application.Abstractions;
using Agentor.Application.Observability;
using Agentor.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Reliability;

public sealed class OutboxDispatcher
{
    private readonly IOutboxStore _store;
    private readonly OutboxDispatcherOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly IRuntimeMetricsRecorder _metrics;

    public OutboxDispatcher(
        IOutboxStore store,
        IOptions<OutboxDispatcherOptions> options,
        ILogger<OutboxDispatcher>? logger = null,
        IRuntimeMetricsRecorder? metrics = null)
    {
        _store = store;
        _options = options.Value;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxDispatcher>.Instance;
        _metrics = metrics ?? NullRuntimeMetricsRecorder.Instance;
    }

    /// <summary>Processes up to <paramref name="batch"/> pending messages. Dispatcher is idempotent per message id.</summary>
    public async Task<int> DispatchPendingAsync(IOutboxSink sink, int batch, CancellationToken cancellationToken)
    {
        var pending = await _store.ListPendingForDispatchAsync(batch, cancellationToken).ConfigureAwait(false);
        var dispatched = 0;
        foreach (var message in pending)
        {
            if (!await _store.TryMarkDispatchingAsync(message.Id, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            _metrics.RecordOutboxDispatchStarted();
            using (_logger.BeginScope(new[]
            {
                new KeyValuePair<string, object?>(AgentorLogFields.OutboxMessageId, message.Id),
            }))
            {
                _logger.LogInformation(AgentorEventIds.OutboxDispatchStarted, "outbox.dispatch.started");
            }

            try
            {
                await sink.SendAsync(message, cancellationToken).ConfigureAwait(false);
                await _store.MarkOutcomeAsync(message.Id, OutboxStatus.Succeeded, null, cancellationToken).ConfigureAwait(false);
                dispatched++;
                _metrics.RecordOutboxDispatchCompleted();
                _logger.LogInformation(AgentorEventIds.OutboxDispatchCompleted, "outbox.dispatch.completed");
            }
            catch (Exception ex)
            {
                _metrics.RecordOutboxDispatchFailed();
                var safe = ObservabilityRedaction.SanitizeExceptionMessage(ex);
                _logger.LogWarning(AgentorEventIds.OutboxDispatchFailed, "outbox.dispatch.failed {Detail}", safe);
                await _store
                    .IncrementAttemptAndRequeueOrPoisonAsync(
                        message.Id,
                        ObservabilityRedaction.SanitizeForLog(ex.Message),
                        _options.MaxDispatchAttempts,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return dispatched;
    }
}
