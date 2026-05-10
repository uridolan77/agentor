namespace Agentor.Application.Redaction;

public sealed record RedactionResult(int RedactedPropertyCount, IReadOnlyList<string> RedactedKeyPaths)
{
    public static RedactionResult Empty { get; } = new(0, []);
}