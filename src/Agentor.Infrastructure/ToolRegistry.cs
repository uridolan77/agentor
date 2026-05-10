using System.Collections.Concurrent;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;

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

    public static ToolRegistry CreateDefault(
        FakeToolExecutor fakeExecutor,
        IModelGatewayClient modelGateway,
        IMcpRegistryClient mcpRegistry,
        IExternalAgentProtocolClient externalAgents)
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
        registry.Register(
            new ToolDefinition(
                WellKnownToolKeys.ConexusModelComplete,
                "Conexus model completion",
                "Text completion via IModelGatewayClient (Conexus port; fake in default infrastructure).",
                ToolRiskLevel.Medium),
            new ModelGatewayToolExecutor(modelGateway));
        registry.Register(
            new ToolDefinition(
                ExternalAgentToolKeys.Discover,
                "External agent capability discovery",
                "Lists capabilities from the configured external-agent protocol adapter (fake by default).",
                ToolRiskLevel.Low),
            new ExternalAgentDiscoverToolExecutor(externalAgents));
        registry.Register(
            new ToolDefinition(
                ExternalAgentToolKeys.Invoke,
                "External agent invocation",
                "Invokes a capability via the external-agent protocol adapter (fake by default).",
                ToolRiskLevel.Medium),
            new ExternalAgentInvokeToolExecutor(externalAgents));
        RegisterDiscoveredMcpTools(registry, mcpRegistry);
        return registry;
    }

    private static void RegisterDiscoveredMcpTools(ToolRegistry registry, IMcpRegistryClient mcpRegistry)
    {
        var servers = mcpRegistry.ListServersAsync(CancellationToken.None).GetAwaiter().GetResult();
        foreach (var server in servers)
        {
            var tools = mcpRegistry.ListToolsAsync(server.Id, CancellationToken.None).GetAwaiter().GetResult();
            foreach (var tool in tools)
            {
                var key = McpToolKeys.Format(server.Id, tool.Name);
                registry.Register(
                    new ToolDefinition(
                        key,
                        $"MCP tool '{tool.Name}'",
                        tool.Description,
                        tool.NominalRisk),
                    new McpToolExecutor(mcpRegistry, server.Id, tool.Name));
            }
        }
    }
}