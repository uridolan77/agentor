using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public sealed record ToolInvocationRegistration(ToolDefinition Definition, IToolExecutor Executor);

public interface IToolRegistry
{
    IReadOnlyList<ToolDefinition> Definitions { get; }

    bool TryGetRegistration(string toolKey, out ToolInvocationRegistration? registration);
}