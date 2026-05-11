using Agentor.Contracts.ExternalAgents;
using Agentor.Domain;
using Agentor.Infrastructure.ExternalAgents;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class FakeA2AExternalAgentClientTests
{
    private readonly FakeA2AExternalAgentClient _sut = new();

    [Fact]
    public async Task ListCapabilities_ReturnsRowsPerCapability()
    {
        var caps = await _sut.ListCapabilitiesAsync(ExternalAgentProtocolKind.A2AStyled);
        Assert.Equal(2, caps.Count);
        Assert.All(caps, c => Assert.Equal(ExternalAgentProtocolKind.A2AStyled, c.ProtocolKind));
    }

    [Fact]
    public async Task Invoke_IsDeterministic()
    {
        var req = new ExternalAgentInvocationRequestDto(
            ExternalAgentProtocolKind.A2AStyled,
            "alpha-agent",
            "reply",
            ToolPayload.FromLegacyDictionary(new Dictionary<string, string> { ["x"] = "1" }));

        var a = await _sut.InvokeAsync(req);
        var b = await _sut.InvokeAsync(req);

        Assert.Equal(a.OutputPayload!.ToPolicyEvaluationDictionary()["artifact"], b.OutputPayload!.ToPolicyEvaluationDictionary()["artifact"]);
    }
}
