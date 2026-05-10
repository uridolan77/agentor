using Agentor.Application.Abstractions;
using Agentor.Application.Reliability;

namespace Agentor.Infrastructure.Reliability;

/// <summary>
/// Default sink used when no transport-backed dispatcher is configured.
/// </summary>
public sealed class NoOpOutboxSink : IOutboxSink
{
    public Task SendAsync(OutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
