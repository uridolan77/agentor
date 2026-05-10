using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.Mcp;

namespace Agentor.Infrastructure.Tests;

public sealed class ToolRegistryMcpBindingTests
{
    [Fact]
    public void CreateDefault_registers_mcp_tools_with_stable_keys()
    {
        var mcp = new FakeMcpRegistryClient();
        var registry = ToolRegistry.CreateDefault(new FakeToolExecutor(), new FakeModelGatewayClient(), mcp);

        var echoKey = McpToolKeys.Format("demo-server", "echo");
        Assert.True(registry.TryGetRegistration(echoKey, out var reg));
        Assert.NotNull(reg);
        Assert.Equal(echoKey, reg!.Definition.Key);
        Assert.IsType<McpToolExecutor>(reg.Executor);
    }

    [Fact]
    public async Task McpToolExecutor_routes_to_fake_registry()
    {
        var mcp = new FakeMcpRegistryClient();
        var registry = ToolRegistry.CreateDefault(new FakeToolExecutor(), new FakeModelGatewayClient(), mcp);
        var key = McpToolKeys.Format("demo-server", "echo");
        Assert.True(registry.TryGetRegistration(key, out var reg));

        var result = await reg!.Executor.ExecuteAsync(
            new ToolExecutionRequest(Guid.NewGuid(), Guid.NewGuid(), key, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["text"] = "bind-test"
            }),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("mcp:demo-server:echo:bind-test", result.Output["result"]);
    }
}
