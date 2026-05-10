using Agentor.Contracts.ExternalAgents;
using Agentor.Infrastructure.ExternalAgents;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeExternalAgentProtocolClientTests
{
    private readonly FakeExternalAgentProtocolClient _sut = new();

    [Fact]
    public async Task ListCapabilities_GenericFake_ReturnsDeterministicRows()
    {
        var caps = await _sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.GenericFake);
        Assert.Equal(2, caps.Count);
        Assert.All(caps, c => Assert.Equal(ExternalAgentProtocolKind.GenericFake, c.ProtocolKind));
        Assert.Contains(caps, c => c.AgentKey == "demo-agent" && c.CapabilityKey == "echo");
    }

    [Fact]
    public async Task ListCapabilities_NonGeneric_ReturnsEmpty()
    {
        var caps = await _sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled);
        Assert.Empty(caps);
    }

    [Fact]
    public async Task Invoke_ProducesDeterministicArtifact_ForSameInputs()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = "1",
            ["b"] = "2",
        };

        var req = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.GenericFake,
            "demo-agent",
            "echo",
            input);

        var r1 = await _sut.InvokeAsync(req);
        var r2 = await _sut.InvokeAsync(req);

        Assert.Equal(ExternalAgentInvocationStatus.Succeeded, r1.Status);
        Assert.NotNull(r1.OutputPayload);
        Assert.True(r1.IsNonCanonEvidence);
        Assert.Equal(r1.OutputPayload!["artifact"], r2.OutputPayload!["artifact"]);
    }

    [Fact]
    public async Task Invoke_MissingKeys_Fails()
    {
        var req = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.GenericFake,
            " ",
            "echo",
            new Dictionary<string, string>());

        var r = await _sut.InvokeAsync(req);
        Assert.Equal(ExternalAgentInvocationStatus.Failed, r.Status);
        Assert.Null(r.OutputPayload);
    }
}
