using Agentor.Infrastructure.Mcp;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeMcpRegistryClientTests
{
    [Fact]
    public async Task ListServersAsync_returns_demo_server()
    {
        var sut = new FakeMcpRegistryClient();

        var servers = await sut.ListServersAsync();

        Assert.Single(servers);
        Assert.Equal("demo-server", servers[0].Id);
        Assert.Contains("Demo", servers[0].DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListToolsAsync_returns_echo_and_stats_for_demo_server()
    {
        var sut = new FakeMcpRegistryClient();

        var tools = await sut.ListToolsAsync("demo-server");

        Assert.Equal(2, tools.Count);
        Assert.Contains(tools, t => string.Equals(t.Name, "echo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tools, t => string.Equals(t.Name, "stats", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListToolsAsync_unknown_server_returns_empty()
    {
        var sut = new FakeMcpRegistryClient();

        var tools = await sut.ListToolsAsync("missing");

        Assert.Empty(tools);
    }

    [Fact]
    public async Task InvokeToolAsync_echo_maps_text_input()
    {
        var sut = new FakeMcpRegistryClient();

        var result = await sut.InvokeToolAsync(
            "demo-server",
            "echo",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["text"] = "hello" });

        Assert.True(result.Success);
        Assert.Equal("mcp:demo-server:echo:hello", result.Output["result"]);
    }

    [Fact]
    public async Task InvokeToolAsync_unknown_server_fails()
    {
        var sut = new FakeMcpRegistryClient();

        var result = await sut.InvokeToolAsync("nope", "echo", new Dictionary<string, string>());

        Assert.False(result.Success);
        Assert.Contains("Unknown MCP server", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
