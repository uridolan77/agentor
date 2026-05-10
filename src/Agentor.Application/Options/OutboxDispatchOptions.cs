namespace Agentor.Application.Options;

public sealed class OutboxDispatchOptions
{
    public const string SectionName = "Agentor:OutboxDispatch";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 20;

    public int PollIntervalMilliseconds { get; set; } = 1000;

    public bool AllowNoOpSinkOutsideDevelopment { get; set; }

    public int SafeBatchSize => Math.Clamp(BatchSize, 1, 500);

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Clamp(PollIntervalMilliseconds, 25, 30000));
}
