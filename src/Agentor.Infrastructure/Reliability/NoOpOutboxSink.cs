using Agentor.Application.Abstractions;
using Agentor.Application.Reliability;

namespace Agentor.Infrastructure.Reliability;

/// <summary>
/// Default sink used for disabled/local/test no-op dispatch scenarios only.
/// </summary>
public sealed class NoOpOutboxSink : IOutboxSink
{
    public Task SendAsync(OutboxMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
