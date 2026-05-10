using System.Collections.Concurrent;
using System.Threading.Channels;
using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Application.RunQueue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.RunQueue;

/// <summary>
/// In-process queue only: not durable across restarts and not broker-backed (PR60.6).
/// </summary>
public sealed class InMemoryRunQueue : IRunQueue
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<RunQueueOptions> _options;
    private readonly ConcurrentDictionary<Guid, QueuedWorkState> _states = new();
    private readonly Channel<RunWorkItem> _channel = Channel.CreateUnbounded<RunWorkItem>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public InMemoryRunQueue(IServiceScopeFactory scopeFactory, IOptionsMonitor<RunQueueOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public ChannelReader<RunWorkItem> Reader => _channel.Reader;

    public Task<RunQueuedWorkSnapshot?> GetSnapshotAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (!_states.TryGetValue(workItemId, out var state))
        {
            return Task.FromResult<RunQueuedWorkSnapshot?>(null);
        }

        return Task.FromResult<RunQueuedWorkSnapshot?>(state.ToSnapshot());
    }

    public async Task EnqueueAsync(RunWorkItem item, CancellationToken cancellationToken)
    {
        _states[item.WorkItemId] = new QueuedWorkState(RunQueuedWorkStatus.Pending);

        if (_options.CurrentValue.ExecutionMode == RunQueueExecutionMode.Inline)
        {
            await ProcessOneAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }

        await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task ProcessOneAsync(RunWorkItem item, CancellationToken cancellationToken)
    {
        if (!_states.TryGetValue(item.WorkItemId, out var state))
        {
            return;
        }

        state.Status = RunQueuedWorkStatus.Running;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<StartAgentRunHandler>();
            var run = await handler.HandleAsync(item.Command, cancellationToken).ConfigureAwait(false);
            state.Status = RunQueuedWorkStatus.Completed;
            state.AgentRunId = run.Id;
        }
        catch (Exception ex)
        {
            state.Status = RunQueuedWorkStatus.Failed;
            state.Error = ex.Message;
        }
    }

    private sealed class QueuedWorkState
    {
        public QueuedWorkState(RunQueuedWorkStatus status) => Status = status;

        public RunQueuedWorkStatus Status { get; set; }

        public Guid? AgentRunId { get; set; }

        public string? Error { get; set; }

        public RunQueuedWorkSnapshot ToSnapshot() => new(Status, AgentRunId, Error);
    }
}
