using System.Collections.Concurrent;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Infrastructure;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, ToolInvocationRegistration> _registrations = new(StringComparer.Ordinal);

    public IReadOnlyList<ToolDefinition> Definitions =>
        _registrations.Values.Select(r => r.Definition).OrderBy(d => d.Key, StringComparer.Ordinal).ToList();

    public bool TryGetRegistration(string toolKey, out ToolInvocationRegistration? registration)
    {
        if (_registrations.TryGetValue(toolKey, out var reg))
        {
            registration = reg;
            return true;
        }

        registration = null;
        return false;
    }

    public void Register(ToolDefinition definition, IToolExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(executor);
        _registrations[definition.Key] = new ToolInvocationRegistration(definition, executor);
    }

    public static ToolRegistry CreateDefault(FakeToolExecutor fakeExecutor)
    {
        var registry = new ToolRegistry();
        registry.Register(
            new ToolDefinition(
                WellKnownToolKeys.Pr1FakeTool,
                "PR1 fake tool",
                "Deterministic local tool for PR1-style runs.",
                ToolRiskLevel.Low),
            fakeExecutor);
        registry.Register(
            new ToolDefinition(
                WellKnownToolKeys.Pr1HighRiskFakeTool,
                "PR1 high-risk fake",
                "Same executor; higher nominal risk for policy tests.",
                ToolRiskLevel.High),
            fakeExecutor);
        return registry;
    }
}