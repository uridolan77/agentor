namespace Agentor.Infrastructure;

public sealed class ToolExecutionOptions
{
    public const string SectionName = "Agentor:ToolExecution";

    /// <summary>Per-attempt timeout. Minimum 1 ms.</summary>
    public int TimeoutMilliseconds { get; set; } = 60_000;

    /// <summary>Total attempts including the first try. Minimum 1.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Delay before each retry after a failed attempt.</summary>
    public int RetryDelayMilliseconds { get; set; } = 0;
}
