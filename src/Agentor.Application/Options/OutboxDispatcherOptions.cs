namespace Agentor.Application.Options;

public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "Agentor:Outbox";

    public int MaxDispatchAttempts { get; set; } = 5;
}
