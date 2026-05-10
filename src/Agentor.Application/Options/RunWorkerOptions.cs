namespace Agentor.Application.Options;

public sealed class RunWorkerOptions
{
    public const string SectionName = "Agentor:RunWorker";

    public bool Enabled { get; set; }

    public int PollIntervalMilliseconds { get; set; } = 1000;

    public int LeaseTtlSeconds { get; set; } = 30;

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(Math.Clamp(PollIntervalMilliseconds, 25, 30000));

    public TimeSpan LeaseTtl => TimeSpan.FromSeconds(Math.Clamp(LeaseTtlSeconds, 1, 600));
}
