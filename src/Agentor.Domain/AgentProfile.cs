namespace Agentor.Domain;

public sealed class AgentProfile
{
    private AgentProfile(Guid id, string name, string purpose, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Purpose = purpose;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Purpose { get; }

    public DateTimeOffset CreatedAt { get; }

    public static AgentProfile Create(string name, string purpose, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Agent profile name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Agent profile purpose is required.", nameof(purpose));
        }

        return new AgentProfile(Guid.NewGuid(), name.Trim(), purpose.Trim(), now);
    }
}
