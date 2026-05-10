using Agentor.Application.Reliability;

namespace Agentor.Application.Abstractions;

/// <summary>Transport for outboxed side effects (Athanor, Conexus, MCP, external agents).</summary>
public interface IOutboxSink
{
    Task SendAsync(OutboxMessage message, CancellationToken cancellationToken);
}
