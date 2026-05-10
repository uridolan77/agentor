namespace Agentor.Domain;

public sealed record SessionMemoryBudget(int MaxKeys, int MaxKeyLength, int MaxValueLength, int MaxTotalStoredCharacters)
{
    public static SessionMemoryBudget Default { get; } = new(64, 128, 8192, 262144);
}
