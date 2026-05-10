namespace Agentor.Application.Options;

public enum RunQueueExecutionMode
{
    /// <summary>Queued work runs on the enqueueing thread before <see cref="IRunQueue.EnqueueAsync"/> returns.</summary>
    Inline = 0,

    /// <summary>Queue-backed background execution driven by <see cref="Agentor.Infrastructure.RunQueue.RunQueueHostedService"/>.</summary>
    InMemoryBackground = 1,

    /// <summary>Durable store; drained by <see cref="Agentor.Infrastructure.RunQueue.RunQueueHostedService"/> when enabled.</summary>
    DurableBackground = 2,
}

public sealed class RunQueueOptions
{
    public const string SectionName = "Agentor:RunQueue";

    public RunQueueExecutionMode ExecutionMode { get; set; } = RunQueueExecutionMode.Inline;
}
