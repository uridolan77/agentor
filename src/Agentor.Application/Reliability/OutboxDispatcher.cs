using Agentor.Application.Abstractions;
using Agentor.Application.Options;

namespace Agentor.Application.Reliability;

public sealed class OutboxDispatcher
{
    private readonly IOutboxStore _store;
    private readonly OutboxDispatcherOptions _options;

    public OutboxDispatcher(IOutboxStore store, Microsoft.Extensions.Options.IOptions<OutboxDispatcherOptions> options)
    {
        _store = store;
        _options = options.Value;
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

            try
            {
                await sink.SendAsync(message, cancellationToken).ConfigureAwait(false);
                await _store.MarkOutcomeAsync(message.Id, OutboxStatus.Succeeded, null, cancellationToken).ConfigureAwait(false);
                dispatched++;
            }
            catch (Exception ex)
            {
                await _store
                    .IncrementAttemptAndRequeueOrPoisonAsync(
                        message.Id,
                        ex.Message,
                        _options.MaxDispatchAttempts,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return dispatched;
    }
}
