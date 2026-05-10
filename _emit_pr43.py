import pathlib
import re
ROOT = pathlib.Path(r"c:/dev/agentor")

pr43_files = {}
pr43_files["src/Agentor.Application/ExternalAgentToolKeys.cs"] = r'''namespace Agentor.Application;

public static class ExternalAgentToolKeys
{
    public const string Discover = "external-agent.discover";

    public const string Invoke = "external-agent.invoke";

    public static bool IsDiscover(string toolKey) =>
        string.Equals(toolKey, Discover, StringComparison.OrdinalIgnoreCase);

    public static bool IsInvoke(string toolKey) =>
        string.Equals(toolKey, Invoke, StringComparison.OrdinalIgnoreCase);

    public static bool IsExternalAgentTool(string toolKey) =>
        IsDiscover(toolKey) || IsInvoke(toolKey);
}
'''

pr43_files["src/Agentor.Infrastructure/ExternalAgents/ExternalAgentDiscoverToolExecutor.cs"] = r'''using System.Globalization;
using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class ExternalAgentDiscoverToolExecutor : IToolExecutor
{
    private readonly IExternalAgentProtocolClient _client;

    public ExternalAgentDiscoverToolExecutor(IExternalAgentProtocolClient client)
    {
        _client = client;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        var kind = ParseProtocolKind(request.Input);
        var caps = await _client.ListCapabilitiesAsync(kind, cancellationToken).ConfigureAwait(false);

        var output = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocolKind"] = kind.ToString(),
            ["capabilityCount"] = caps.Count.ToString(CultureInfo.InvariantCulture),
            ["toolKey"] = request.ToolKey,
            ["nonCanon"] = "true",
        };

        for (var i = 0; i < caps.Count; i++)
        {
            var c = caps[i];
            output[$"capability.{i}.protocolKind"] = c.ProtocolKind.ToString();
            output[$"capability.{i}.agentKey"] = c.AgentKey;
            output[$"capability.{i}.capabilityKey"] = c.CapabilityKey;
            output[$"capability.{i}.summary"] = c.Summary;
        }

        return new ToolExecutionResult(true, output);
    }

    private static ExternalAgentProtocolKind ParseProtocolKind(IReadOnlyDictionary<string, string> input)
    {
        if (!input.TryGetValue("protocolKind", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return ExternalAgentProtocolKind.Unspecified;
        }

        return Enum.TryParse<ExternalAgentProtocolKind>(raw.Trim(), ignoreCase: true, out var k)
            ? k
            : ExternalAgentProtocolKind.Unspecified;
    }
}
'''

pr43_files["src/Agentor.Infrastructure/ExternalAgents/ExternalAgentInvokeToolExecutor.cs"] = r'''using System.Linq;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;

namespace Agentor.Infrastructure.ExternalAgents;

public sealed class ExternalAgentInvokeToolExecutor : IToolExecutor
{
    private readonly IExternalAgentProtocolClient _client;

    public ExternalAgentInvokeToolExecutor(IExternalAgentProtocolClient client)
    {
        _client = client;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        if (!request.Input.TryGetValue("agentKey", out var agentKey) || string.IsNullOrWhiteSpace(agentKey))
        {
            return new ToolExecutionResult(false, null, "agentKey is required.");
        }

        if (!request.Input.TryGetValue("capabilityKey", out var capabilityKey) || string.IsNullOrWhiteSpace(capabilityKey))
        {
            return new ToolExecutionResult(false, null, "capabilityKey is required.");
        }

        var kind = ParseProtocolKind(request.Input);
        var passthrough = request.Input
            .Where(kv => !IsReserved(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        var dto = new ExternalAgentInvocationRequestDto(kind, agentKey.Trim(), capabilityKey.Trim(), passthrough);
        var result = await _client.InvokeAsync(dto, cancellationToken).ConfigureAwait(false);

        if (result.Status != ExternalAgentInvocationStatus.Succeeded || result.OutputPayload is null)
        {
            return new ToolExecutionResult(false, null, result.ErrorMessage ?? "External agent invocation failed.");
        }

        var output = new Dictionary<string, string>(result.OutputPayload, StringComparer.OrdinalIgnoreCase)
        {
            ["toolKey"] = request.ToolKey,
            ["protocolKind"] = kind.ToString(),
            ["invocationStatus"] = result.Status.ToString(),
            ["nonCanon"] = "true",
        };

        return new ToolExecutionResult(true, output);
    }

    private static bool IsReserved(string key) =>
        string.Equals(key, "protocolKind", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "agentKey", StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, "capabilityKey", StringComparison.OrdinalIgnoreCase);

    private static ExternalAgentProtocolKind ParseProtocolKind(IReadOnlyDictionary<string, string> input)
    {
        if (!input.TryGetValue("protocolKind", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return ExternalAgentProtocolKind.Unspecified;
        }

        return Enum.TryParse<ExternalAgentProtocolKind>(raw.Trim(), ignoreCase: true, out var k)
            ? k
            : ExternalAgentProtocolKind.Unspecified;
    }
}
'''

for rel, content in pr43_files.items():
    (ROOT / rel).parent.mkdir(parents=True, exist_ok=True)
    (ROOT / rel).write_text(content, encoding="utf-8")

# Patch ToolRegistry.cs
tr = ROOT / "src/Agentor.Infrastructure/ToolRegistry.cs"
text = tr.read_text(encoding="utf-8")
if "ExternalAgentToolKeys" not in text:
    text = text.replace(
        "using Agentor.Infrastructure.Mcp;",
        "using Agentor.Infrastructure.ExternalAgents;\nusing Agentor.Infrastructure.Mcp;",
    )
    old_sig = """    public static ToolRegistry CreateDefault(
        FakeToolExecutor fakeExecutor,
        IModelGatewayClient modelGateway,
        IMcpRegistryClient mcpRegistry)
    {
        var registry = new ToolRegistry();"""
    new_sig = """    public static ToolRegistry CreateDefault(
        FakeToolExecutor fakeExecutor,
        IModelGatewayClient modelGateway,
        IMcpRegistryClient mcpRegistry,
        IExternalAgentProtocolClient externalAgents)
    {
        var registry = new ToolRegistry();"""
    text = text.replace(old_sig, new_sig)
    insert_after_model = """            new ModelGatewayToolExecutor(modelGateway));
        RegisterDiscoveredMcpTools(registry, mcpRegistry);"""
    insert_new = """            new ModelGatewayToolExecutor(modelGateway));
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
        RegisterDiscoveredMcpTools(registry, mcpRegistry);"""
    text = text.replace(insert_after_model, insert_new)
tr.write_text(text, encoding="utf-8")

# Patch DependencyInjection IToolRegistry
di = ROOT / "src/Agentor.Infrastructure/DependencyInjection.cs"
dt = di.read_text(encoding="utf-8")
dt = dt.replace(
    """services.AddSingleton<IToolRegistry>(sp => ToolRegistry.CreateDefault(
            sp.GetRequiredService<FakeToolExecutor>(),
            sp.GetRequiredService<IModelGatewayClient>(),
            sp.GetRequiredService<IMcpRegistryClient>()));""",
    """services.AddSingleton<IToolRegistry>(sp => ToolRegistry.CreateDefault(
            sp.GetRequiredService<FakeToolExecutor>(),
            sp.GetRequiredService<IModelGatewayClient>(),
            sp.GetRequiredService<IMcpRegistryClient>(),
            sp.GetRequiredService<IExternalAgentProtocolClient>()));""",
)
di.write_text(dt, encoding="utf-8")

# Patch all tests / files using CreateDefault(
for path in ROOT.rglob("*.cs"):
    if "obj" in path.parts or "bin" in path.parts:
        continue
    t = path.read_text(encoding="utf-8")
    if "ToolRegistry.CreateDefault(" not in t:
        continue
    # append external agent client argument
    t2 = re.sub(
        r"ToolRegistry\.CreateDefault\(\s*([^,]+),\s*([^,]+),\s*([^)]+)\)",
        r"ToolRegistry.CreateDefault(\1, \2, \3, new FakeA2AExternalAgentClient())",
        t,
    )
    if t2 != t:
        path.write_text(t2, encoding="utf-8")

print("patched tool registry and call sites")
