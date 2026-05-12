using System.Net;
using Agentor.Infrastructure.Http;
using Ontogony.Contracts.Events;
using Ontogony.Http;
using Ontogony.Observability;
using Xunit;

namespace Agentor.Infrastructure.Tests;

/// <summary>
/// Ensures outbound integration handler order matches DI: Ontogony canonical headers, then legacy Agentor trace alias.
/// </summary>
public sealed class OntogonyOutboundCorrelationHeadersTests
{
    [Fact]
    public async Task Correlation_then_legacy_handler_adds_both_trace_headers()
    {
        var traceId = "a1b2c3d4e5f6789012345678abcdef01";
        using var _ = OntogonyCorrelationContext.Push(traceId);

        var recording = new RecordingHandler();
        var legacy = new AgentorLegacyTraceHeaderHandler { InnerHandler = recording };
        var correlation = new CorrelationHeadersDelegatingHandler { InnerHandler = legacy };

        using var invoker = new HttpMessageInvoker(correlation);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://athanor.test/api/ping");
        await invoker.SendAsync(request, CancellationToken.None);

        Assert.NotNull(recording.Captured);
        Assert.True(
            recording.Captured!.Headers.TryGetValues(OntogonyEventHeaders.TraceId, out var ont),
            "X-Ontogony-Trace-Id missing");
        Assert.Equal(traceId, Assert.Single(ont));

        Assert.True(
            recording.Captured.Headers.TryGetValues(OntogonyEventHeaders.LegacyAgentorTraceId, out var agentor),
            "X-Agentor-Trace-Id missing");
        Assert.Equal(traceId, Assert.Single(agentor));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
