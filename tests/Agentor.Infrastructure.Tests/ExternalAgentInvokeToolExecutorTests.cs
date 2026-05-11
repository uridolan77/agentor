using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Contracts.ExternalAgents;
using Agentor.Domain;
using Agentor.Infrastructure.ExternalAgents;
using Xunit;

namespace Agentor.Infrastructure.Tests;

public sealed class ExternalAgentInvokeToolExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_maps_isNonCanonEvidence_onto_tool_output()
    {
        var inner = new StubExternalClient(
            new ExternalAgentInvocationResultDto(
                ExternalAgentInvocationStatus.Succeeded,
                ToolPayload.FromLegacyDictionary(new Dictionary<string, string> { ["artifact"] = "x" }),
                null,
                IsNonCanonEvidence: false));

        var sut = new ExternalAgentInvokeToolExecutor(inner);
        var result = await sut.ExecuteAsync(
            new ToolExecutionRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ExternalAgentToolKeys.Invoke,
                ToolPayload.FromLegacyDictionary(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["protocolKind"] = nameof(ExternalAgentProtocolKind.GenericFake),
                    ["agentKey"] = "a",
                    ["capabilityKey"] = "c",
                })),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("false", result.Output.ToPolicyEvaluationDictionary()["isNonCanonEvidence"]);
    }

    private sealed class StubExternalClient(ExternalAgentInvocationResultDto response) : IExternalAgentProtocolClient
    {
        public Task<IReadOnlyList<ExternalAgentCapabilityDto>> ListCapabilitiesAsync(
            ExternalAgentProtocolKind protocolKind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ExternalAgentCapabilityDto>>([]);

        public Task<ExternalAgentInvocationResultDto> InvokeAsync(
            ExternalAgentInvocationRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }
}
