namespace Agentor.Domain;

public sealed class ToolDefinition
{
    public ToolDefinition(string key, string displayName, string description, string version, bool isDeterministic)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Tool key is required.", nameof(key));
        }

        Key = key.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName.Trim();
        Description = description.Trim();
        Version = string.IsNullOrWhiteSpace(version) ? "v1" : version.Trim();
        IsDeterministic = isDeterministic;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public string Version { get; }

    public bool IsDeterministic { get; }
}
