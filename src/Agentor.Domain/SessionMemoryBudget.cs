namespace Agentor.Domain;

/// <summary>
/// Caps for AgentRun.TryWriteSessionMemory (run scratch only; not Athanor or canon knowledge).
/// </summary>
public sealed record SessionMemoryBudget(int MaxKeys, int MaxKeyLength, int MaxValueLength, int MaxTotalStoredCharacters)
{
    public static SessionMemoryBudget Default { get; } = new(64, 128, 8192, 262144);
}