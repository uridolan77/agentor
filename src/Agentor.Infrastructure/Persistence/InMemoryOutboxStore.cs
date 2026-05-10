using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Application.Reliability;

namespace Agentor.Infrastructure.Persistence;

public sealed class InMemoryOutboxStore : IOutboxStore
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();
    private readonly object _orderLock = new();

    public Task AppendAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> ListPendingForDispatchAsync(int take, CancellationToken cancellationToken)
    {
        lock (_orderLock)
        {
            var list = _messages.Values
                .Where(m => m.Status == OutboxStatus.Pending)
                .OrderBy(m => m.CreatedAt)
                .Take(take)
                .Select(m => m)
                .ToList();
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(list);
        }
    }

    public Task<IReadOnlyList<OutboxMessage>> ListLatestAsync(int take, CancellationToken cancellationToken)
    {
        lock (_orderLock)
        {
            var list = _messages.Values
                .OrderByDescending(m => m.CreatedAt)
                .Take(Math.Clamp(take, 1, 500))
                .ToList();
            return Task.FromResult<IReadOnlyList<OutboxMessage>>(list);
        }
    }

    public Task<bool> TryMarkDispatchingAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_orderLock)
        {
            if (!_messages.TryGetValue(id, out var m))
            {
                return Task.FromResult(false);
            }

            if (m.Status is not OutboxStatus.Pending)
            {
                return Task.FromResult(false);
            }

            _messages[id] = m with { Status = OutboxStatus.Dispatching };
            return Task.FromResult(true);
        }
    }

    public Task MarkOutcomeAsync(Guid id, OutboxStatus status, string? detail, CancellationToken cancellationToken)
    {
        lock (_orderLock)
        {
            if (_messages.TryGetValue(id, out var m))
            {
                _messages[id] = m with { Status = status, LastError = detail ?? m.LastError };
            }
        }

        return Task.CompletedTask;
    }

    public Task IncrementAttemptAndRequeueOrPoisonAsync(Guid id, string error, int maxAttempts, CancellationToken cancellationToken)
    {
        lock (_orderLock)
        {
            if (!_messages.TryGetValue(id, out var m))
            {
                return Task.CompletedTask;
            }

            var next = m.AttemptCount + 1;
            if (next >= maxAttempts)
            {
                _messages[id] = m with { Status = OutboxStatus.Poison, AttemptCount = next, LastError = error };
            }
            else
            {
                _messages[id] = m with { Status = OutboxStatus.Pending, AttemptCount = next, LastError = error };
            }
        }

        return Task.CompletedTask;
    }
}
