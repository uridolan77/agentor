using Agentor.Application.Mcp;
using Xunit;

namespace Agentor.Application.Tests;

/// <summary>
/// Phase 21 contract reminder: MCP catalog descriptors stay in Application, not Domain.
/// </summary>
public sealed class McpDescriptorDomainBoundaryTests
{
    [Fact]
    public void McpServerDescriptor_lives_in_application_layer_not_domain()
    {
        Assert.StartsWith("Agentor.Application", typeof(McpServerDescriptor).Namespace ?? "", StringComparison.Ordinal);
        Assert.StartsWith("Agentor.Application", typeof(McpToolDescriptor).Namespace ?? "", StringComparison.Ordinal);
    }
}
